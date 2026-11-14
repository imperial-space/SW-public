using System.Linq;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaSpectatorSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _views = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaSpectatorAnchorComponent, EntityTerminatingEvent>(OnAnchorTerminating);
        SubscribeLocalEvent<BossArenaSpectatorOrbComponent, ComponentShutdown>(OnOrbShutdown);
        Subs.BuiEvents<BossArenaSpectatorOrbComponent>(BossArenaSpectatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
        });
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRefresh) return;
        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        Refresh();
    }

    public void Refresh()
    {
        var arenas = EntityQueryEnumerator<BossArenaMapComponent>();
        while (arenas.MoveNext(out var arena, out var map))
        {
            var owner = map.EntryGate is { } gate && Live(gate) ? gate : map.Entrance;
            if (!Live(owner)) continue;
            var anchor = EnsureComp<BossArenaSpectatorAnchorComponent>(owner);
            anchor.Arena = arena;
            if (!anchor.Enabled || !TryGetActiveArena(owner, anchor, out _))
            {
                RemoveOrb(anchor);
                continue;
            }
            if (anchor.Orb is { } existing &&
                TryComp<BossArenaSpectatorOrbComponent>(existing, out var current) &&
                current.Arena == arena && Live(current.Camera)) continue;
            RemoveOrb(anchor);
            var parent = Transform(owner).GridUid ?? Transform(owner).MapUid;
            if (parent == null) continue;
            var position = _transform.ToCoordinates(parent.Value,
                _transform.ToMapCoordinates(new EntityCoordinates(owner, anchor.Offset)));
            var orb = Spawn(anchor.OrbPrototype, position);
            var spectator = EnsureComp<BossArenaSpectatorOrbComponent>(orb);
            spectator.Anchor = owner;
            spectator.Arena = arena;
            spectator.Camera = Spawn("BossArenaSpectatorCamera", new EntityCoordinates(map.CombatGrid, 0.5f, 0.5f));
            anchor.Orb = orb;
        }
        var orbs = EntityQueryEnumerator<BossArenaSpectatorOrbComponent>();
        while (orbs.MoveNext(out var orb, out var spectator))
        {
            if (!Live(spectator.Anchor) || !TryComp<BossArenaSpectatorAnchorComponent>(spectator.Anchor, out var gate) ||
                gate.Orb != orb || !TryGetActiveArena(spectator.Anchor, gate, out var arena) || arena != spectator.Arena)
            {
                Cleanup(orb, spectator);
                QueueDel(orb);
                continue;
            }
            foreach (var viewer in spectator.Viewers.ToArray())
            {
                if (CanWatch(orb, spectator, viewer.Key) && viewer.Value.AttachedEntity == viewer.Key &&
                    _ui.GetActors(orb, BossArenaSpectatorUiKey.Key).Contains(viewer.Key)) continue;
                _ui.CloseUi(orb, BossArenaSpectatorUiKey.Key, viewer.Key);
                RemoveViewer(spectator, viewer.Key);
            }
        }
    }

    private bool Live(EntityUid uid) => Exists(uid) && !Terminating(uid);

    private bool TryGetActiveArena(EntityUid owner, BossArenaSpectatorAnchorComponent anchor, out EntityUid arena)
    {
        arena = anchor.Arena;
        return Live(owner) && anchor.Enabled && Live(arena) &&
            TryComp<BossArenaMapComponent>(arena, out var map) &&
            Live(map.Boss) && TryComp<BossEncounterComponent>(map.Boss, out var encounter) &&
            encounter.FightRunning && encounter.IdleDeadline == null && map.VictoryDeadline == null;
    }

    private bool CanWatch(EntityUid orb, BossArenaSpectatorOrbComponent spectator, EntityUid user)
        => Live(orb) && Live(spectator.Camera) && Live(user) &&
           TryComp<BossArenaSpectatorAnchorComponent>(spectator.Anchor, out var gate) && gate.Orb == orb &&
           TryGetActiveArena(spectator.Anchor, gate, out var arena) && arena == spectator.Arena &&
           Transform(user).MapUid == Transform(orb).MapUid && !HasComp<GhostComponent>(user) && _mobs.IsAlive(user) &&
           TryComp<ActorComponent>(user, out var actor) && actor.PlayerSession.AttachedEntity == user &&
           actor.PlayerSession.Status is SessionStatus.Connected or SessionStatus.InGame &&
           _interaction.InRangeUnobstructed(user, orb);

    private void OnOpened(EntityUid orb, BossArenaSpectatorOrbComponent spectator, BoundUIOpenedEvent args)
    {
        if (!CanWatch(orb, spectator, args.Actor))
        {
            _ui.CloseUi(orb, BossArenaSpectatorUiKey.Key, args.Actor);
            return;
        }
        var session = Comp<ActorComponent>(args.Actor).PlayerSession;
        if (spectator.Viewers.TryGetValue(args.Actor, out var old) && old != session) RemoveViewer(spectator, args.Actor);
        spectator.Viewers[args.Actor] = session;                                                 // each viewer gets their own subscription
        _views.AddViewSubscriber(spectator.Camera, session);
        _ui.SetUiState(orb, BossArenaSpectatorUiKey.Key, new BossArenaSpectatorUiState(GetNetEntity(spectator.Camera)));
    }

    private void OnClosed(EntityUid orb, BossArenaSpectatorOrbComponent spectator, BoundUIClosedEvent args)
        => RemoveViewer(spectator, args.Actor);

    private void RemoveViewer(BossArenaSpectatorOrbComponent spectator, EntityUid user)
    {
        if (!spectator.Viewers.Remove(user, out var session)) return;
        if (Exists(spectator.Camera)) _views.RemoveViewSubscriber(spectator.Camera, session);    // remove this viewer, leave the others watching
    }

    private void Cleanup(EntityUid orb, BossArenaSpectatorOrbComponent spectator)
    {
        _ui.CloseUi(orb, BossArenaSpectatorUiKey.Key);
        foreach (var user in spectator.Viewers.Keys.ToArray()) RemoveViewer(spectator, user);    // drop all subscriptions before deleting the camera
        if (Exists(spectator.Camera)) QueueDel(spectator.Camera);
    }

    private void RemoveOrb(BossArenaSpectatorAnchorComponent anchor)
    {
        if (anchor.Orb is not { } orb) return;
        anchor.Orb = null;
        if (TryComp<BossArenaSpectatorOrbComponent>(orb, out var spectator)) Cleanup(orb, spectator);
        if (Exists(orb)) QueueDel(orb);
    }

    private void OnAnchorTerminating(EntityUid uid, BossArenaSpectatorAnchorComponent anchor, ref EntityTerminatingEvent args) => RemoveOrb(anchor);
    private void OnOrbShutdown(EntityUid uid, BossArenaSpectatorOrbComponent spectator, ComponentShutdown args) => Cleanup(uid, spectator);
}
