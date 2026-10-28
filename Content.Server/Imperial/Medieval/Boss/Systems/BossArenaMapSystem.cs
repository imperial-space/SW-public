using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaMapSystem : EntitySystem
{
    public const int RecoveryHealth = 5;

    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossArenaChasmComponent, StepTriggerAttemptEvent>(OnChasmAttempt);
        SubscribeLocalEvent<BossArenaChasmComponent, StepTriggeredOffEvent>(OnChasmStep);
        SubscribeLocalEvent<BossDefeatedEvent>(OnBossDefeated);
        SubscribeLocalEvent<BossArenaMapComponent, MapInitEvent>(OnArenaInit);
        SubscribeLocalEvent<BossArenaFallingComponent, ComponentShutdown>(OnFallShutdown);
    }

    private void OnArenaInit(EntityUid uid, BossArenaMapComponent component, MapInitEvent args)
    {
        EnsureComp<BossArenaRulesComponent>(uid);
        ApplyAmbientLight(new(uid, component));
        EntityManager.System<BossMechanicSystem>().EnsureBraziers(new(uid, component));
    }

    private void ApplyAmbientLight(Entity<BossArenaMapComponent> arena)
    {

        if (TryComp<BossMechanicComponent>(arena.Comp.Boss, out var mechanic) && mechanic.Blackout) return;
        _maps.SetAmbientLight(Transform(arena.Owner).MapID, arena.Comp.AmbientLight);
    }

    public bool TryEnsureArena(EntityUid gate, BossArenaGateComponent component)
    {
        if (component.ReturnToGate || component.ArenaMapPath == null && component.ArenaPrototype == null ||
            component.ArenaMapPath != null && component.ArenaPrototype != null)
            return false;

        var arenaId = component.ArenaPrototype?.Id ?? component.ArenaId;
        var arenas = EntityQueryEnumerator<BossArenaMapComponent>();
        while (arenas.MoveNext(out var arenaUid, out var arena))
        {
            if (arena.ArenaId != arenaId || arena.ArenaPrototype != component.ArenaPrototype || !ValidArena(arenaUid, arena) ||
                arena.EntryGate is { } owner && owner != gate && Exists(owner) && !Terminating(owner))
                continue;
            var mapId = Transform(arenaUid).MapID;
            if (!_maps.IsInitialized(mapId))
                _maps.InitializeMap(mapId);
            _maps.SetPaused(mapId, false);
            ApplyAmbientLight(new(arenaUid, arena));
            return EntityManager.System<BossArenaGateSystem>().LinkGate(gate, arena.Entrance);
        }

        if (component.ArenaPrototype is { } prototype)
        {
            if (!EntityManager.System<BossArenaGenerationSystem>().TryCreate(prototype, out var generated))
                return false;
            if (EntityManager.System<BossArenaGateSystem>().LinkGate(gate, generated.Comp.Entrance))
                return true;
            _maps.DeleteMap(Transform(generated.Owner).MapID);
            return false;
        }

        if (component.ArenaMapPath is not { } path ||
            !_loader.TryLoadMap(path, out var map, out _, new DeserializationOptions { InitializeMaps = true }))
            return false;
        if (!TryComp<BossArenaMapComponent>(map.Value.Owner, out var loaded) ||
            loaded.ArenaId != arenaId || !ValidArena(map.Value.Owner, loaded))
        {
            _maps.DeleteMap(map.Value.Comp.MapId);
            return false;
        }
        _maps.SetPaused(map.Value.Comp.MapId, false);
        return EntityManager.System<BossArenaGateSystem>().LinkGate(gate, loaded.Entrance);
    }

    private bool ValidArena(EntityUid uid, BossArenaMapComponent arena)
        => Exists(uid) && !Terminating(uid) && Transform(uid).MapID != MapId.Nullspace &&
           arena.HalfSize is >= 2 and <= 32 && !string.IsNullOrWhiteSpace(arena.ArenaId) &&
           Exists(arena.Entrance) && !Terminating(arena.Entrance) &&
           HasComp<BossArenaEntranceComponent>(arena.Entrance) && Transform(arena.Entrance).MapUid == uid &&
           HasComp<MapGridComponent>(arena.CombatGrid) && Transform(arena.CombatGrid).MapUid == uid;

    public void BindGate(EntityUid gate, EntityUid entrance)
    {
        if (Transform(entrance).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena) ||
            arena.Entrance != entrance || Transform(gate).MapUid == map)
            return;
        if (arena.EntryGate == null || !Exists(arena.EntryGate.Value) || Terminating(arena.EntryGate.Value))
            arena.EntryGate = gate;
        EntityManager.System<BossEncounterSystem>().AttachGate(arena.Boss, gate);
        if (arena.VictoryExit is { } exit && Exists(exit) && TryComp<BossArenaGateComponent>(exit, out var exitGate))
        {
            exitGate.Destination = arena.EntryGate;
            exitGate.Revision++;
            EntityManager.System<BossArenaGateSystem>().SetOpen(exit, true);
        }
    }

    public void RecordEntry(EntityUid user, EntityUid gate, EntityUid entrance)
    {
        if (Transform(entrance).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena))
            return;
        EnsureComp<BossArenaVisitorComponent>(user).Gate = gate;
        EnsureComp<BossArenaPartyMemberComponent>(user);
        EntityManager.System<BossEncounterSystem>().Join(arena.Boss, user);
    }

    public List<EntityUid> GetParty(EntityUid map)
    {
        var result = new List<EntityUid>();
        var rules = EntityManager.System<SharedBossArenaRulesSystem>();
        var query = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (xform.MapUid == map && !Terminating(uid) && rules.IsPartyBody(uid))
                result.Add(uid);
        result.Sort();
        return result;
    }

    public bool HasEntrySpace(EntityUid destination, EntityUid user)
    {
        if (Transform(destination).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena))
            return true;
        var party = GetParty(map);
        return party.Contains(user) || party.Count < arena.MaxPlayers;
    }

    public void LeaveArena(EntityUid user, EntityUid? arenaMap)
    {
        if (TryComp<BossEncounterParticipantComponent>(user, out var fight) && fight.Boss is { } netBoss)
        {
            var resolved = TryGetEntity(netBoss, out var boss);
            if (!resolved || Transform(boss!.Value).MapUid == arenaMap)
            {
                if (resolved && TryComp<BossComponent>(boss, out var component))
                    component.Players.Remove(user);
                RemComp<BossEncounterParticipantComponent>(user);
            }
        }
        RemComp<BossArenaVisitorComponent>(user);
        RemComp<BossArenaPartyMemberComponent>(user);
    }

    private bool IsFallingBody(EntityUid uid)
        => Exists(uid) && !Terminating(uid) && !HasComp<GhostComponent>(uid) &&
           HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid) &&
           EntityManager.System<SharedBossArenaRulesSystem>().IsPartyBody(uid);

    private void OnChasmAttempt(EntityUid uid, BossArenaChasmComponent component, ref StepTriggerAttemptEvent args)
        => args.Continue = IsFallingBody(args.Tripper);

    private void OnChasmStep(EntityUid uid, BossArenaChasmComponent component, ref StepTriggeredOffEvent args)
    {
        if (Transform(uid).MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena))
            TryReturnFromPit(args.Tripper, map, arena);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var falling = EntityQueryEnumerator<BossArenaFallingComponent>();
        while (falling.MoveNext(out var user, out var fall))
        {
            if (MetaData(user).EntityPaused) continue;
            if (!TryComp<BossArenaMapComponent>(fall.Arena, out var arena) || Transform(user).MapUid != fall.Arena)
            { CancelFall(user); continue; }
            if (_timing.CurTime >= fall.ReturnAt)
            {
                if (!FinishPitReturn(user, fall.Arena, arena)) CancelFall(user);
            }
        }
        if (_timing.CurTime < _nextCheck)
            return;
        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(0.1);

        var query = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var user, out _, out var xform))
        {
            if (xform.MapUid is { } map && TryComp<BossArenaMapComponent>(map, out var arena) && !IsOnPlatform(user, arena))
                TryReturnFromPit(user, map, arena);
        }
    }

    private bool IsOnPlatform(EntityUid user, BossArenaMapComponent arena)
    {
        if (!TryComp<MapGridComponent>(arena.CombatGrid, out var grid))
            return true;
        var position = _transform.ToCoordinates(arena.CombatGrid, _transform.ToMapCoordinates(Transform(user).Coordinates)).Position;
        return !_maps.GetTileRef(arena.CombatGrid, grid,
            new Vector2i((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y))).Tile.IsEmpty;
    }

    public bool TryReturnFromPit(EntityUid user, EntityUid map, BossArenaMapComponent arena)
    {
        if (!Exists(user) || Terminating(user) || HasComp<GhostComponent>(user) ||
            !EntityManager.System<SharedBossArenaRulesSystem>().IsPartyBody(user) ||
            Transform(user).MapUid != map || IsOnPlatform(user, arena) ||
            (HasComp<ChasmFallingComponent>(user) || HasComp<BossArenaFallVisualComponent>(user)) ||
            !HasComp<DamageableComponent>(user) ||
            !_thresholds.TryGetDeadThreshold(user, out var threshold) || threshold.Value < 1)
            return false;
        var gates = EntityManager.System<BossArenaGateSystem>();
        if (!Exists(arena.Entrance) || Terminating(arena.Entrance) ||
            !HasComp<BossArenaEntranceComponent>(arena.Entrance))
            return false;

        if (TryComp<DoAfterComponent>(user, out var actions))
            foreach (var id in actions.DoAfters.Keys.ToArray())
                _doAfter.Cancel(user, id, actions);
        gates.StopMotion(user);
        var animation = AddComp<BossArenaFallVisualComponent>(user);
        var fall = AddComp<BossArenaFallingComponent>(user);
        fall.Arena = map;
        fall.ReturnAt = _timing.CurTime + animation.ReturnDelay;
        _blocker.UpdateCanMove(user);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/falling.ogg"), user);
        return true;
    }

    public void CancelFall(EntityUid user) => RemComp<BossArenaFallingComponent>(user);

    private void OnFallShutdown(EntityUid user, BossArenaFallingComponent component, ComponentShutdown args)
    {
        if (Terminating(user)) return;
        RemComp<BossArenaFallVisualComponent>(user);
        _blocker.UpdateCanMove(user);
    }

    private bool FinishPitReturn(EntityUid user, EntityUid map, BossArenaMapComponent arena)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable) ||
            !_thresholds.TryGetDeadThreshold(user, out var threshold) || threshold.Value < 1 ||
            !TryComp<BossArenaEntranceComponent>(arena.Entrance, out var entrance) ||
            Transform(arena.Entrance).MapUid != map) return false;
        var gates = EntityManager.System<BossArenaGateSystem>();
        var destination = _transform.ToCoordinates(arena.CombatGrid,
            _transform.ToMapCoordinates(new EntityCoordinates(arena.Entrance, entrance.ArrivalOffset)));
        CancelFall(user);
        gates.TransportUser(user, destination);
        var damage = new DamageSpecifier(damageable.Damage);
        damage.DamageDict["Blunt"] = damage.DamageDict.GetValueOrDefault("Blunt") +
            FixedPoint2.Max(FixedPoint2.Zero, threshold.Value + FixedPoint2.New(1) - damageable.TotalDamage);
        _damage.SetDamage(user, damageable, damage);
        _mobState.ChangeMobState(user, Content.Shared.Mobs.MobState.Dead);
        return true;
    }

    public void SetCenterBlocked(Entity<BossArenaMapComponent> arena, bool blocked)
    {
        if (!Exists(arena.Comp.CombatGrid) || Terminating(arena.Comp.CombatGrid)) return;
        var blockers = EntityManager.AllEntities<BossArenaCenterBlockerComponent>()
            .Where(ent => !Terminating(ent.Owner) && Transform(ent.Owner).GridUid == arena.Comp.CombatGrid)
            .Select(ent => ent.Owner).ToArray();
        if (!blocked)
        {
            foreach (var blocker in blockers) Del(blocker);
        }
        else if (blockers.Length == 0 && arena.Comp.CenterBlockerPrototype is { } prototype)
            Spawn(prototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
    }

    private void OnBossDefeated(ref BossDefeatedEvent args)
    {
        if (Transform(args.Boss).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena) ||
            !ValidArena(map, arena) || arena.Boss != args.Boss)
            return;
        SetCenterBlocked(new(map, arena), false);
        if (arena.VictoryExit is { } previous && Exists(previous) && !Terminating(previous))
            return;

        if (arena.VictoryExitPrototype is not { } exitPrototype) return;
        var tile = arena.VictoryExitTile;
        var exit = Spawn(exitPrototype, new EntityCoordinates(arena.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
        arena.VictoryExit = exit;
        var timer = Comp<BossArenaExitTimerComponent>(exit);
        timer.EndTime = arena.VictoryDeadline ?? _timing.CurTime + arena.VictoryLootDuration;
        Dirty(exit, timer);
        var exitGate = Comp<BossArenaGateComponent>(exit);
        exitGate.Destination = arena.EntryGate;
        var gates = EntityManager.System<BossArenaGateSystem>();
        var portals = EntityQueryEnumerator<BossArenaGateComponent>();
        while (portals.MoveNext(out var entry, out var portal))
            if (!portal.ReturnToGate && portal.Destination == arena.Entrance)
                gates.SetOpen(entry, true);
        if (arena.EntryGate is { } fallback && Exists(fallback) && !Terminating(fallback))
            gates.SetOpen(exit, true);
    }
}
