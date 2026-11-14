using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem : EntitySystem
{
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedDoAfterSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BossEncounterComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<BossEncounterComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
    }

    private void OnDamage(EntityUid uid, BossEncounterComponent comp, DamageChangedEvent args)
    {
        if (!comp.AutoStart || comp.FightRunning || !args.DamageIncreased || _rules.ResolveSource(args.Origin) == null ||
            !TryComp<BossComponent>(uid, out var boss) || boss.Finished || boss.Health <= 0 || !_telegraphs.TryGetArena(uid, out _))
            return;
        EntityManager.System<BossSystem>().StartBossfight(_telegraphs.LivingPlayers(uid), uid);
    }

    public void Begin(EntityUid boss)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !_telegraphs.TryGetArena(boss, out var arena))
            return;
        comp.Generation++;
        comp.FightRunning = true;
        var started = new BossFightEvent(boss, true);
        RaiseLocalEvent(ref started);
        comp.Repeating = false;
        comp.Escalating = false;
        ClearPending(comp);
        comp.LastMixedStackFirst = null;
        comp.Waiting = false;
        comp.Step = comp.Lap = 0;
        comp.IdleDeadline = null;
        comp.RainPressureStart = null;
        comp.RunningStep = null;
        comp.Party.Clear();                                                                        // start a fresh party list for this boss
        comp.Party.UnionWith(Comp<BossComponent>(boss).Players);

        comp.Party.UnionWith(EntityManager.AllEntities<MobStateComponent>()
            .Where(player => _rules.IsPartyBody(player.Owner) && Transform(player.Owner).MapUid == arena.Owner)
            .Select(player => player.Owner));
        comp.EntryCloses = _timing.CurTime + TimeSpan.FromSeconds(comp.EntryGrace);
        comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.OpeningDelay); // schedule the opening mechanic
        comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
        SetGateLocks(boss, true, false);                                                           // late entries still get the grace window
    }

    public void Join(EntityUid boss, EntityUid player)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning || comp.IdleDeadline != null ||
            _timing.CurTime >= comp.EntryCloses || !_telegraphs.TryGetPlayerTile(boss, player, out _))
            return;
        comp.Party.Add(player);
        var encounter = Comp<BossComponent>(boss);
        if (!encounter.Players.Contains(player)) encounter.Players.Add(player);
        var fighting = EnsureComp<FightingBossComponent>(player);
        fighting.Boss = GetNetEntity(boss);
        Dirty(player, fighting);
    }





    private void SetGateLocks(EntityUid boss, bool combat, bool entry)
    {
        if (!_telegraphs.TryGetArena(boss, out var arena)) return;
        var state = new BossArenaStateEvent(arena, BossArenaState.GateLocks)
        {
            CombatLocked = combat,
            EntryLocked = entry,
        };
        RaiseLocalEvent(ref state);
    }

    public void End(EntityUid boss)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp)) return;
        var stopped = new BossFightEvent(boss, false);
        comp.FightRunning = false;
        comp.RunningStep = null;
        RaiseLocalEvent(ref stopped);
        EntityManager.System<BossSystem>().CancelBossCast(boss);                 // a stopped flow must not leave a delayed hit running
        EntityManager.System<BossActionSystem>().Cleanup(boss);
        comp.Waiting = false;
        comp.IdleDeadline = null;
        ClearPending(comp);
        comp.Generation++;
        SetGateLocks(boss, false, false);
    }

    private void OnTerminating(EntityUid uid, BossEncounterComponent comp, ref EntityTerminatingEvent args)
    {
        End(uid);
    }

    public override void Update(float frameTime)
    {
        UpdateVictory();
        UpdateEncounters(frameTime);
    }
}
