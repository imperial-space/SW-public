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
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _views = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaGateComponent, EntityTerminatingEvent>(OnGateTerminating);
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
        var gates = EntityQueryEnumerator<BossArenaGateComponent>();
        while (gates.MoveNext(out var gate, out var component))
        {
            if (component.SpectatorOrbPrototype is not { } prototype || !TryGetActiveArena(gate, component, out var arena))
            {
                RemoveOrb(component);
                continue;
            }
            if (component.SpectatorOrb is { } existing &&
                TryComp<BossArenaSpectatorOrbComponent>(existing, out var current) &&
                current.Arena == arena && Live(current.Camera)) continue;
            RemoveOrb(component);
            var orb = Spawn(prototype, new EntityCoordinates(gate, component.SpectatorOrbOffset));
            var spectator = EnsureComp<BossArenaSpectatorOrbComponent>(orb);
            spectator.Gate = gate;
            spectator.Arena = arena;
            spectator.Camera = Spawn("BossArenaSpectatorCamera", new EntityCoordinates(Comp<BossArenaMapComponent>(arena).CombatGrid, 0.5f, 0.5f));
            component.SpectatorOrb = orb;
        }
        var orbs = EntityQueryEnumerator<BossArenaSpectatorOrbComponent>();
        while (orbs.MoveNext(out var orb, out var spectator))
        {
            if (!Live(spectator.Gate) || !TryComp<BossArenaGateComponent>(spectator.Gate, out var gate) ||
                gate.SpectatorOrb != orb || !TryGetActiveArena(spectator.Gate, gate, out var arena) || arena != spectator.Arena)
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

    private bool TryGetActiveArena(EntityUid gate, BossArenaGateComponent component, out EntityUid arena)
    {
        arena = default;
        if (!Live(gate) || component.ReturnToGate || component.Destination is not { } entrance || !Live(entrance) ||
            Transform(entrance).MapUid is not { } map || Transform(gate).MapUid == map ||
            !TryComp<BossArenaMapComponent>(map, out var arenaComponent) || arenaComponent.Entrance != entrance ||
            !Live(arenaComponent.Boss) || !TryComp<BossEncounterComponent>(arenaComponent.Boss, out var encounter) ||
            !encounter.FightRunning || encounter.IdleDeadline != null || arenaComponent.VictoryDeadline != null)
            return false;
        arena = map;
        return true;
    }

    private bool CanWatch(EntityUid orb, BossArenaSpectatorOrbComponent spectator, EntityUid user)
        => Live(orb) && Live(spectator.Camera) && Live(user) &&
           TryComp<BossArenaGateComponent>(spectator.Gate, out var gate) && gate.SpectatorOrb == orb &&
           TryGetActiveArena(spectator.Gate, gate, out var arena) && arena == spectator.Arena &&
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
        spectator.Viewers[args.Actor] = session;
        _views.AddViewSubscriber(spectator.Camera, session);
        _ui.SetUiState(orb, BossArenaSpectatorUiKey.Key, new BossArenaSpectatorUiState(GetNetEntity(spectator.Camera)));
    }

    private void OnClosed(EntityUid orb, BossArenaSpectatorOrbComponent spectator, BoundUIClosedEvent args)
        => RemoveViewer(spectator, args.Actor);

    private void RemoveViewer(BossArenaSpectatorOrbComponent spectator, EntityUid user)
    {
        if (!spectator.Viewers.Remove(user, out var session)) return;
        if (Exists(spectator.Camera)) _views.RemoveViewSubscriber(spectator.Camera, session);
    }

    private void Cleanup(EntityUid orb, BossArenaSpectatorOrbComponent spectator)
    {
        _ui.CloseUi(orb, BossArenaSpectatorUiKey.Key);
        foreach (var user in spectator.Viewers.Keys.ToArray()) RemoveViewer(spectator, user);
        if (Exists(spectator.Camera)) QueueDel(spectator.Camera);
    }

    private void RemoveOrb(BossArenaGateComponent gate)
    {
        if (gate.SpectatorOrb is not { } orb) return;
        gate.SpectatorOrb = null;
        if (TryComp<BossArenaSpectatorOrbComponent>(orb, out var spectator)) Cleanup(orb, spectator);
        if (Exists(orb)) QueueDel(orb);
    }

    private void OnGateTerminating(EntityUid uid, BossArenaGateComponent gate, ref EntityTerminatingEvent args) => RemoveOrb(gate);
    private void OnOrbShutdown(EntityUid uid, BossArenaSpectatorOrbComponent spectator, ComponentShutdown args) => Cleanup(uid, spectator);
}
