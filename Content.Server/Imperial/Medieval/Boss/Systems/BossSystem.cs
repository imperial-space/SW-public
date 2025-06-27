using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAttacks();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBoss();
        UpdateSpiked();
        UpdateMark();
    }

    public void StartBossfight(IEnumerable<EntityUid> players, EntityUid boss, EntityUid grid)
    {
        var bossComp = EnsureComp<BossComponent>(boss);

        List<EntityUid> positions = new();
        while (Transform(grid).ChildEnumerator.MoveNext(out var child))
        {
            if (_tag.HasTag(child, (ProtoId<TagPrototype>)"BossSpawnPosition"))
            {
                positions.Add(child);
            }
        }

        foreach (var player in players)
        {
            bossComp.Players.Add(player);

            _transform.SetCoordinates(player, Transform(_random.Pick(positions)).Coordinates);
            EnsureComp<FightingBossComponent>(player);
        }
    }

    public void DamageBoss(EntityUid boss, float damage)
    {
        if (!TryComp<BossComponent>(boss, out var bossComp))
            return;

        bossComp.Health -= damage;

        var min = bossComp.Stages.Where(x => x.Value.Threshold >= bossComp.Health).Select(x => x.Key).Min();
        if (bossComp.Stage != min)
        {
            var stage = bossComp.Stages[bossComp.Stage];
            _appearance.SetData(boss, BossStageVisuals.Stage, bossComp.Stage);

            if (bossComp.Stage > min)
                _audio.PlayPvs(stage.Sound, boss);

            bossComp.Stage = min;
        }

        if (bossComp.Health <= 0)
        {
            var beforeEv = new BeforeBossDefeatEvent();
            RaiseLocalEvent(boss, ref beforeEv);

            if (beforeEv.Cancelled)
                return;

            var ev = new BossDefeatedEvent(boss);
            RaiseLocalEvent(ref ev);
        }
    }

    private List<BossAttack> GetBossAttacks(BossComponent comp)
    {
        List<BossAttack> attacks = new();

        foreach (var stage in comp.Stages.Where(x => x.Key <= comp.Stage))
            attacks.AddRange(stage.Value.Attacks.Where(x => x.NextAttack <= _timing.CurTime));

        return attacks;
    }

    private void UpdateBoss()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (bossComp.Players.Count == 0 || !bossComp.Active)
                continue;

            var list = GetBossAttacks(bossComp);
            var stage = bossComp.Stages[bossComp.Stage];
            if (!list.Any())
                continue;

            var count = _random.Pick(new List<int>() { stage.AttacksPerTime.Item1, stage.AttacksPerTime.Item2 });

            _random.Shuffle(list);
            for (var i = 0; i < count && i < list.Count; i++)
            {
                var attack = list[i];
                if (attack.NextAttack > _timing.CurTime)
                    continue;

                if (attack.Execute(uid, bossComp.Players, EntityManager))
                    attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            }

            bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
        }
    }
}
