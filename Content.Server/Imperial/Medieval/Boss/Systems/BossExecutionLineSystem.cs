using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossExecutionLineSystem : EntitySystem, IBossActionFeature
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly SharedBossArenaRulesSystem _rules = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        EntityManager.System<BossActionSystem>().Register(this);
        SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
        SubscribeLocalEvent<BossWonEvent>(OnWon);
    }

    public bool Enabled(EntityUid boss)
        => TryComp<BossTelegraphSettingsComponent>(boss, out var settings) && (settings.ExecutionLine ||
            EntityManager.System<BossMechanicDefinitionSystem>().Slot(boss, BossActionSlot.Execution) != null);

    public EntityUid? Start(EntityUid boss, EntityUid target, TimeSpan start, TimeSpan impact)
    {
        if (!TryComp<BossTelegraphSettingsComponent>(boss, out var settings) || !Enabled(boss) ||
            !float.IsFinite(settings.ExecutionLineWidth) || settings.ExecutionLineWidth <= 0 ||
            impact <= start || !_telegraphs.TryGetArena(boss, out var arena) || !ValidTarget(boss, target)) return null;
        var root = Spawn(null, new EntityCoordinates(arena.Comp.CombatGrid, Vector2.Zero));
        var line = AddComp<BossExecutionLineComponent>(root);
        line.Boss = boss;
        line.Target = target;
        line.Grid = arena.Comp.CombatGrid;
        line.HalfSize = arena.Comp.HalfSize;
        line.Width = settings.ExecutionLineWidth;
        line.Style = EntityManager.System<BossMechanicDefinitionSystem>().Slot(boss, BossActionSlot.Execution)?.Style ?? settings.Style;
        line.StartTime = start;
        line.ImpactTime = impact;
        line.EndTime = impact + TimeSpan.FromSeconds(BossExecutionLineVisuals.Duration);
        Track(line);
        Dirty(root, line);
        return root;
    }

    private bool ValidTarget(EntityUid boss, EntityUid target)
        => Exists(target) && !Terminating(target) && _rules.IsPartyBody(target) && _mobs.IsAlive(target) &&
           Transform(target).MapUid == Transform(boss).MapUid;

    private void Track(BossExecutionLineComponent line)
    {
        var inverse = _transform.GetInvWorldMatrix(line.Grid);
        line.Origin = Vector2.Transform(_transform.GetWorldPosition(line.Boss), inverse);
        var target = Vector2.Transform(_transform.GetWorldPosition(line.Target), inverse);
        line.Direction = BossExecutionLineVisuals.Aim(line.Origin, target, line.Direction);
        line.Length = BossExecutionLineVisuals.LengthToEdge(line.Origin, line.Direction, line.HalfSize); // stop the line at the arena edge
    }

    public bool Resolve(EntityUid boss, EntityUid target, DamageSpecifier damage)
    {
        var query = EntityQueryEnumerator<BossExecutionLineComponent>();
        while (query.MoveNext(out var uid, out var line))
        {
            if (line.Boss != boss || line.Target != target || line.Resolved || Terminating(uid)) continue;
            if (!ValidTarget(boss, target) || !_telegraphs.TryGetArena(boss, out _))
            {
                Cancel(uid);
                return false;
            }
            Track(line);
            line.Resolved = true;                                                // this line can only deal damage once
            line.ImpactTime = _timing.CurTime;
            line.EndTime = _timing.CurTime + TimeSpan.FromSeconds(BossExecutionLineVisuals.Duration);
            Dirty(uid, line);
            _telegraphs.PlayTankBusterSound(boss);

            var players = EntityManager.AllEntities<MobStateComponent>()
                .Where(player => ValidTarget(boss, player.Owner)).Select(player => player.Owner).ToArray();
            foreach (var player in players)
            {
                var position = Vector2.Transform(_transform.GetWorldPosition(player), _transform.GetInvWorldMatrix(line.Grid));
                if (!BossExecutionLineVisuals.Contains(position, line.Origin, line.Direction, line.Length, line.Width)) continue; // hit anyone inside the line, not just the tank
                var received = EntityManager.System<DamageableSystem>().TryChangeDamage(player, damage, origin: boss);
                EntityManager.System<BossHitFeedbackSystem>().OnHit(boss, player, received, tankBuster: player == target);
            }
            return true;
        }
        return false;
    }

    public void Cancel(EntityUid? effect)
    {
        if (effect is { } uid && Exists(uid) && !Terminating(uid)) QueueDel(uid);
    }

    public void Clear(EntityUid boss)
    {
        foreach (var effect in EntityManager.AllEntities<BossExecutionLineComponent>().ToArray())
            if (effect.Comp.Boss == boss) Cancel(effect.Owner);
    }
    private void OnDefeated(ref BossDefeatedEvent args) => Clear(args.Boss);
    private void OnWon(ref BossWonEvent args) => Clear(args.Boss);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossExecutionLineComponent>();
        while (query.MoveNext(out var uid, out var line))
        {
            if (MetaData(uid).EntityPaused || Terminating(uid)) continue;
            if (!TryComp<BossComponent>(line.Boss, out var boss) || !boss.Active || boss.Finished || boss.Health <= 0 ||
                !_telegraphs.TryGetArena(line.Boss, out _) || !Exists(line.Grid))
            {
                Cancel(uid);
                continue;
            }
            if (line.Resolved)
            {
                if (_timing.CurTime >= line.EndTime) Cancel(uid);
                continue;
            }
            if (!TryComp<BossCastingComponent>(line.Boss, out var cast) || cast.ExecutionLine != uid || !ValidTarget(line.Boss, line.Target))
            {
                Cancel(uid);
                EntityManager.System<BossSystem>().CancelBossCast(line.Boss);
                continue;
            }
            Track(line);
            Dirty(uid, line);
        }
    }
}
