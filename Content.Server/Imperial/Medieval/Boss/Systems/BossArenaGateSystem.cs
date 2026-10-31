using System.Linq;
using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaGateSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly BossArenaMapSystem _arena = default!;
    [Dependency] private readonly IMapManager _maps = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossArenaGateComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BossArenaGateComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BossArenaGateComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<BossArenaGateComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BossArenaGateComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BossArenaGateComponent, BossArenaGateDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BossArenaGateComponent, DoAfterAttemptEvent<BossArenaGateDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<BossArenaEntranceComponent, EntityTerminatingEvent>(OnEntranceTerminating);
    }

    private void OnStartup(EntityUid uid, BossArenaGateComponent component, ComponentStartup args)
        => UpdateState(uid, component);

    private void OnMapInit(EntityUid uid, BossArenaGateComponent component, MapInitEvent args)
        => UpdateState(uid, component);

    public bool LinkGate(EntityUid gate, EntityUid destination)
    {
        if (!TryComp<BossArenaGateComponent>(gate, out var component) || !ValidEntrance(destination))
            return false;

        if (!component.ReturnToGate && BelongsToAnotherGate(gate, destination))
            return _arena.TryEnsureArena(gate, component);

        if (component.Destination != destination)
            component.Revision++;                                               // old channels shouldn't finish on a new destination
        component.Destination = destination;
        _arena.BindGate(gate, destination);
        UpdateState(gate, component);
        return true;
    }

    private bool BelongsToAnotherGate(EntityUid gate, EntityUid? destination)
        => destination is { } entrance && ValidEntrance(entrance) &&
           Transform(entrance).MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena) &&
           arena.EntryGate is { } owner && owner != gate && Exists(owner) && !Terminating(owner);

    public bool SetOpen(EntityUid gate, bool open)
    {
        if (!TryComp<BossArenaGateComponent>(gate, out var component) ||
            (open && (component.CombatLocked || !ValidDestination(component, component.Destination))))
            return false;

        var changed = component.Open != open;
        component.Open = open;
        if (changed)
            component.Revision++;
        UpdateState(gate, component);
        if (changed)
            RaiseLocalEvent(gate, new BossArenaGateStateChangedEvent(open));
        return true;
    }

    public bool TryEnter(EntityUid gate, EntityUid user)
    {
        if (!TryComp<BossArenaGateComponent>(gate, out var component) || !component.Open ||
            !ValidDestination(component, ResolveDestination(user, component)) || !CanUse(gate, user))
            return false;

        if (!component.ReturnToGate && BelongsToAnotherGate(gate, component.Destination) &&
            !_arena.TryEnsureArena(gate, component))
            return false;
        if (!TryGetDestinationPosition(user, component, ResolveDestination(user, component)!.Value, out _))
            return false;

        if (!component.ReturnToGate && component.Destination is { } entrance && Transform(entrance).MapUid is { } map &&
            TryComp<BossArenaMapComponent>(map, out var arena) &&
            !EntityManager.System<BossEncounterSystem>().PrepareRetry(new(map, arena))) return false;

        return StartAction(gate, component, user, null);
    }

    public bool TryUnlock(EntityUid gate, EntityUid user, EntityUid? key = null)
    {
        if (!TryComp<BossArenaGateComponent>(gate, out var component) || component.Open ||
            component.ReturnToGate || component.CombatLocked || InVictoryWindow(component) ||
            (!ValidEntrance(component.Destination) && component.ArenaMapPath == null) ||
            !CanUse(gate, user) || !Access(gate, component, user, key).Allowed)
            return false;

        return StartAction(gate, component, user, key, unlocking: true);
    }

    private bool StartAction(EntityUid gate, BossArenaGateComponent component, EntityUid user, EntityUid? key, bool unlocking = false)
    {
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user,
            unlocking ? component.UnlockDuration : component.EntryDuration,
            new BossArenaGateDoAfterEvent(ResolveDestination(user, component) is { } destination
                ? GetNetEntity(destination) : NetEntity.Invalid, component.Revision, unlocking),
            gate, target: gate, used: key)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = unlocking,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            AttemptFrequency = AttemptFrequency.EveryTick,

            DuplicateCondition = DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = true
        });
    }

    private bool CanUse(EntityUid gate, EntityUid user)
    {
        if (TryComp<BossArenaGateComponent>(gate, out var locked) && locked.EntryLocked) return false;

        if (TryComp<BossArenaGateComponent>(gate, out var capacity) && !capacity.ReturnToGate &&
            capacity.Destination is { } arenaEntrance && !_arena.HasEntrySpace(arenaEntrance, user))
            return false;
        if (TryComp<BossArenaGateComponent>(gate, out var entrance) && !entrance.ReturnToGate &&
            entrance.Destination is { } destination &&
            EntityManager.System<Content.Shared.Imperial.Medieval.Boss.SharedBossArenaRulesSystem>().IsArena(destination) &&
            !EntityManager.System<Content.Shared.Imperial.Medieval.Boss.SharedBossArenaRulesSystem>().CanBringEquipment(user, destination))
        {
            _popup.PopupEntity(Loc.GetString("Boss_Encounter_Rules_No_Furniture"), user, user);
            return false;
        }
        return Exists(user) && LifeStage(user) < EntityLifeStage.Terminating &&
               TryComp<ActorComponent>(user, out var actor) &&
               actor.PlayerSession.Status is SessionStatus.Connected or SessionStatus.InGame &&
               _mobState.IsAlive(user) && !HasComp<GhostComponent>(user) && !HasComp<BossLegacyParticipantComponent>(user) && !Transform(user).Anchored &&
               !_containers.IsEntityOrParentInContainer(user) &&
               !(TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled) &&
               !(TryComp<BossArenaGateTransitComponent>(user, out var transit) && transit.NextEntry > _timing.CurTime) &&
               _interaction.InRangeUnobstructed(user, gate);
    }

    private BossArenaAccessEvent Access(EntityUid gate, BossArenaGateComponent component, EntityUid user, EntityUid? item)
    {
        var request = new BossArenaAccessEvent(gate, user, item, component.ArenaId);
        RaiseLocalEvent(ref request);
        return request;
    }

    private bool InVictoryWindow(BossArenaGateComponent component)
        => component.Destination is { } destination && Exists(destination) &&
           Transform(destination).MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena) &&
           arena.VictoryDeadline != null;

    private bool ValidAction(EntityUid gate, BossArenaGateComponent component, DoAfterArgs args, BossArenaGateDoAfterEvent ev)
    {
        var destination = ResolveDestination(args.User, component);
        var validDestination = ValidDestination(component, destination) ||
                               (ev.Unlock && !component.ReturnToGate && destination == null &&
                                (component.ArenaMapPath != null));
        return component.Revision == ev.Revision &&                             // check the gate again when the channel finishes
               component.Open != ev.Unlock &&
               validDestination &&
               (destination is { } uid ? GetNetEntity(uid) : NetEntity.Invalid) == ev.Destination &&
               CanUse(gate, args.User) &&
               (!ev.Unlock || !component.CombatLocked && !InVictoryWindow(component)) &&
               (!ev.Unlock || Access(gate, component, args.User, args.Used).Allowed);
    }

    private void OnDoAfterAttempt(EntityUid uid, BossArenaGateComponent component, DoAfterAttemptEvent<BossArenaGateDoAfterEvent> args)
    {
        if (!ValidAction(uid, component, args.DoAfter.Args, args.Event))
            args.Cancel();
    }

    private void OnDoAfter(EntityUid uid, BossArenaGateComponent component, BossArenaGateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !ValidAction(uid, component, args.Args, args))
            return;
        args.Handled = true;

        if (args.Unlock)
        {
            if ((!ValidEntrance(component.Destination) || BelongsToAnotherGate(uid, component.Destination)) &&
                !_arena.TryEnsureArena(uid, component))
            {
                _popup.PopupEntity(Loc.GetString(component.MessagePrefix + "Gate_Arena_Failed"), uid, args.User);
                return;
            }
            var access = Access(uid, component, args.User, args.Args.Used);
            if (!access.Allowed || !SetOpen(uid, true)) return;
            if (access.ConsumeItem && args.Args.Used is { } key)
                Del(key);                                                       // only spend the key after the gate opens
            _popup.PopupEntity(Loc.GetString(component.MessagePrefix + "Gate_Unlocked"), uid, args.User);
            return;
        }

        var destination = ResolveDestination(args.User, component);
        if (destination == null || !TryGetDestinationPosition(args.User, component, destination.Value, out var target))
        {
            _popup.PopupEntity(Loc.GetString(component.MessagePrefix + "Gate_Arrival_Blocked"), uid, args.User);
            return;
        }

        var source = Transform(args.User).Coordinates;
        var sourceMap = Transform(args.User).MapUid;
        TransportUser(args.User, target);
        if (component.ReturnToGate)
            _arena.LeaveArena(args.User, sourceMap);
        else
            _arena.RecordEntry(args.User, uid, destination.Value);
        _adminLog.Add(LogType.Teleport, LogImpact.Low,
            $"{ToPrettyString(args.User):player} used {ToPrettyString(uid)} from {source} to {ToPrettyString(destination.Value)} at {target}");
    }

    public void TransportUser(EntityUid user, EntityCoordinates target)
        => EntityManager.System<BossArenaTransportSystem>().TransportUser(user, target);

    public void StopMotion(EntityUid user)
        => EntityManager.System<BossArenaTransportSystem>().StopMotion(user);

    private EntityUid? ResolveDestination(EntityUid user, BossArenaGateComponent component)
    {
        if (component.ReturnToGate && TryComp<BossArenaVisitorComponent>(user, out var visitor) &&
            ValidReturnGate(visitor.Gate))
            return visitor.Gate;
        return component.Destination;
    }

    private bool ValidDestination(BossArenaGateComponent component, EntityUid? destination)
        => component.ReturnToGate ? ValidReturnGate(destination) : ValidEntrance(destination);

    private bool ValidReturnGate(EntityUid? destination)
        => destination is { } uid && Exists(uid) && !Terminating(uid) &&
           TryComp<BossArenaGateComponent>(uid, out var gate) && !gate.ReturnToGate &&
           Transform(uid).MapID != MapId.Nullspace;

    private bool TryGetDestinationPosition(EntityUid user, BossArenaGateComponent component,
        EntityUid destination, out EntityCoordinates target)
        => component.ReturnToGate ? TryGetGateFront(user, destination, out target) : TryGetArrival(user, destination, out target);

    public bool TryGetGateFront(EntityUid user, EntityUid gate, out EntityCoordinates target)
    {
        target = default;
        if (!ValidReturnGate(gate))
            return false;

        foreach (var depth in new[] { -1.5f, -2.25f, -3f })
            foreach (var lateral in new[] { 0f, -0.75f, 0.75f, -1.5f, 1.5f })
            {
                var mapPoint = _transform.ToMapCoordinates(new EntityCoordinates(gate, new Vector2(lateral, depth)));
                if (!_maps.TryFindGridAt(mapPoint.MapId, mapPoint.Position, out var grid, out _) ||
                    !TryGetClearPosition(user, grid, mapPoint, out target))
                    continue;
                return true;
            }
        return false;
    }

    public bool TryGetPlatformRecoveryPosition(EntityUid user, EntityUid grid, out EntityCoordinates target)
        => EntityManager.System<BossArenaTransportSystem>().TryGetPlatformRecoveryPosition(user, grid, out target);

    private bool ValidEntrance(EntityUid? destination)
    {
        return destination is { } uid && Exists(uid) && LifeStage(uid) < EntityLifeStage.Terminating &&
               HasComp<BossArenaEntranceComponent>(uid) && Transform(uid).MapID != MapId.Nullspace;
    }

    private bool TryGetArrival(EntityUid user, EntityUid destination, out EntityCoordinates target)
    {
        var entrance = Comp<BossArenaEntranceComponent>(destination);
        target = default;
        foreach (var depth in new[] { 0f, 0.75f, 1.5f })
            foreach (var lateral in new[] { 0f, -0.9f, 0.9f, -1.8f, 1.8f, -2.7f, 2.7f, -3.6f, 3.6f })
            {
                var mapTarget = _transform.ToMapCoordinates(new EntityCoordinates(destination,
                    entrance.ArrivalOffset + new Vector2(lateral, -depth)));
                var parent = Transform(destination).ParentUid;
                if (EntityManager.System<SharedBossArenaRulesSystem>().IsArena(destination) &&
                    (!_maps.TryFindGridAt(mapTarget.MapId, mapTarget.Position, out var grid, out _) ||
                     !TryComp<BossArenaMapComponent>(Transform(destination).MapUid, out var arena) || grid != arena.CombatGrid))
                    continue;
                if (TryGetClearPosition(user, parent, mapTarget, out target))
                    return true;
            }
        return false;
    }

    private bool TryGetClearPosition(EntityUid user, EntityUid parent, MapCoordinates mapTarget, out EntityCoordinates target)
        => EntityManager.System<BossArenaTransportSystem>().TryGetClearPosition(user, parent, mapTarget, out target);

    private void UpdateState(EntityUid uid, BossArenaGateComponent component)
    {
        var open = component.Open && ValidDestination(component, component.Destination);
        _appearance.SetData(uid, BossArenaGateVisuals.Open, open);
        if (TryComp<FixturesComponent>(uid, out var fixtures) &&
            _fixtures.GetFixtureOrNull(uid, "gate", fixtures) is { } fixture)

            _physics.SetHard(uid, fixture, true);
    }

    private void OnInteract(EntityUid uid, BossArenaGateComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        EnterWithFeedback(uid, component, args.User);
    }

    private void OnActivate(EntityUid uid, BossArenaGateComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        args.WasLogged = true;
        EnterWithFeedback(uid, component, args.User);
    }

    private void OnInteractUsing(EntityUid uid, BossArenaGateComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (component.Open)
        {
            EnterWithFeedback(uid, component, args.User);
            return;
        }
        if (component.CombatLocked)
            _popup.PopupEntity(Loc.GetString(component.MessagePrefix + "Gate_Combat_Locked"), uid, args.User);
        else if (!TryUnlock(uid, args.User, args.Used))
            _popup.PopupEntity(Loc.GetString(component.MessagePrefix + "Gate_Key_Required"), uid, args.User);
    }

    private void EnterWithFeedback(EntityUid uid, BossArenaGateComponent component, EntityUid user)
    {
        if (!component.ReturnToGate && component.Destination is { } entrance && !_arena.HasEntrySpace(entrance, user))
        {
            _popup.PopupEntity(Loc.GetString("boss-arena-party-full"), uid, user);
            return;
        }
        if (!component.Open && !component.ReturnToGate && TryUnlock(uid, user)) return;
        if (!TryEnter(uid, user))
            _popup.PopupEntity(Loc.GetString(component.CombatLocked && (!component.Open || component.EntryLocked)
                ? component.MessagePrefix + "Gate_Combat_Locked"
                : component.Open ? component.MessagePrefix + "Gate_Unavailable" : component.MessagePrefix + "Gate_Closed"), uid, user);
    }

    private void OnEntranceTerminating(EntityUid uid, BossArenaEntranceComponent component, ref EntityTerminatingEvent args)
    {
        var query = AllEntityQuery<BossArenaGateComponent>();
        while (query.MoveNext(out var gate, out var gateComp))
        {
            if (gateComp.Destination != uid || LifeStage(gate) >= EntityLifeStage.Terminating)
                continue;

            gateComp.Destination = null;
            gateComp.Revision++;
            SetOpen(gate, false);
        }
    }
}
