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

    public void StartBossfight(List<EntityUid> players, EntityUid boss)
    {
        var bossComp = EnsureComp<BossComponent>(boss);

        var grid = Transform(boss).GridUid;
        if (grid == null)
            return;

        List<EntityUid> positions = new();
        var enumerator = Transform(grid.Value).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
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

        bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(13);
        bossComp.Active = true;
    }

    public void DamageBoss(EntityUid boss, float damage)
    {
        if (!TryComp<BossComponent>(boss, out var bossComp))
            return;

        bossComp.Health -= damage;

        var max = bossComp.Stages.Where(x => x.Value.Threshold >= bossComp.Health).Select(x => x.Key).Max();
        if (bossComp.Stage != max)
        {
            var stage = bossComp.Stages[bossComp.Stage];
            _appearance.SetData(boss, BossStageVisuals.Stage, max);

            if (bossComp.Stage < max)
                _audio.PlayPvs(stage.Sound, boss);

            bossComp.Stage = max;
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
            attacks.AddRange(stage.Value.Attacks);

        return attacks;
    }

    private void UpdateBoss()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (bossComp.Players.Count == 0 || !bossComp.Active)
                continue;

            if (bossComp.NextAttack > _timing.CurTime)
                continue;

            var list = GetBossAttacks(bossComp);
            var stage = bossComp.Stages[bossComp.Stage];
            if (!list.Any())
                continue;

            var count = _random.Pick(new List<int>() { stage.AttacksPerTime.Item1, stage.AttacksPerTime.Item2 });

            _random.Shuffle(list);
            for (var i = 0; i < count && i < list.Count; i++)
            {
                var attack = PickAttack(list);
                if (attack == null)
                    break;

                if (attack.NextAttack > _timing.CurTime)
                    continue;

                if (attack.Execute(uid, bossComp.Players, EntityManager))
                    attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            }

            bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
        }
    }

    private BossAttack? PickAttack(List<BossAttack> list)
    {
        var picks = list.Select(x => (x, x.Priority)).ToDictionary();
        var sum = picks.Values.Sum();
        var accumulated = 0f;

        var rand = _random.NextFloat() * sum;

        foreach (var (key, weight) in picks)
        {
            accumulated += weight;

            if (accumulated >= rand)
            {
                list.Remove(key);
                return key;
            }
        }

        return null;
    }
}
