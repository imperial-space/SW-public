using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaGateFeatureSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaStateEvent>(OnState);
        SubscribeLocalEvent<BossArenaPlayerEvent>(OnPlayer);
        SubscribeLocalEvent<BossArenaReturnEvent>(OnReturn);
    }

    private void OnState(ref BossArenaStateEvent args)
    {
        if (args.State == BossArenaState.Victory)
            EntityManager.System<BossArenaMapSystem>().CreateVictoryExit(args.Arena);
        var query = EntityQueryEnumerator<BossArenaGateComponent>();
        while (query.MoveNext(out var uid, out var gate))
        {
            if (gate.ReturnToGate || gate.Destination != args.Arena.Comp.Entrance) continue;
            if (args.State == BossArenaState.GateLocks)
            {
                if (gate.EntryLocked != args.EntryLocked) gate.Revision++;
                gate.CombatLocked = args.CombatLocked;
                gate.EntryLocked = args.EntryLocked;
                if (args.EntryLocked) EntityManager.System<BossArenaGateSystem>().SetOpen(uid, false);
            }
            if (args.State == BossArenaState.Closed)
                EntityManager.System<BossArenaGateSystem>().SetOpen(uid, false);
            if (args.State == BossArenaState.Reopened)
                EntityManager.System<BossArenaGateSystem>().SetOpen(uid, true);
        }
    }

    private void OnPlayer(ref BossArenaPlayerEvent args)
    {
        if (args.Change == BossArenaPlayerChange.Joined && args.Owner is { } gate)
            EnsureComp<BossArenaVisitorComponent>(args.Player).Gate = gate;
        if (args.Change == BossArenaPlayerChange.Left)
            RemComp<BossArenaVisitorComponent>(args.Player);
        if (args.Change == BossArenaPlayerChange.Transported)
            EnsureComp<BossArenaGateTransitComponent>(args.Player).NextEntry = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnReturn(ref BossArenaReturnEvent args)
    {
        var gate = TryComp<BossArenaVisitorComponent>(args.Player, out var visitor) ? visitor.Gate : args.Arena.Comp.EntryGate;
        if (gate == null) return;
        args.Valid = EntityManager.System<BossArenaGateSystem>().TryGetGateFront(args.Player, gate.Value, out var destination);
        if (!args.Valid && args.AllowFallbackOwner && args.Arena.Comp.EntryGate is { } fallback)
            args.Valid = EntityManager.System<BossArenaGateSystem>().TryGetGateFront(args.Player, fallback, out destination);
        if (args.Valid) args.Destination = destination;
    }
}
