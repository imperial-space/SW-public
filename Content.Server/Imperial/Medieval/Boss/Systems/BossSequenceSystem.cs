using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSequenceSystem : EntitySystem, IBossActionFeature
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly BossTelegraphSystem _telegraphs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize()
    {
        EntityManager.System<BossActionSystem>().Register(this);
        SubscribeLocalEvent<BossSequenceComponent, ComponentShutdown>(OnShutdown);
    }

    public static bool IsCleave(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.Sinistra or BossTelegraphPattern.Dextra or
            BossTelegraphPattern.Frons or BossTelegraphPattern.Tergum;

    public static bool ValidQueue(IReadOnlyList<BossTelegraphPattern> queue)
        => queue.Count is >= 2 and <= 4 && queue.All(IsCleave) &&
           !Enumerable.Range(2, queue.Count - 2).Any(i => queue[i] == queue[i - 1] && queue[i] == queue[i - 2]);

    public void RecordStrike(EntityUid boss, BossTelegraphPattern direction)
        => EntityManager.System<BossAttackHistorySystem>().RecordStrike(boss, direction);
    public bool CanFollowHistory(EntityUid boss, IReadOnlyList<BossTelegraphPattern> queue)
        => EntityManager.System<BossAttackHistorySystem>().CanFollowHistory(boss, queue);
    public static BossTelegraphPattern TransformDirection(BossTelegraphPattern pattern, int rotation, bool mirror)
        => BossAttackHistorySystem.TransformDirection(pattern, rotation, mirror);

    private List<BossTelegraphPattern>? PickQueue(EntityUid boss, BossPreparedSequenceAttack attack)
    {
        if (attack.RandomDirections)
        {
            var weights = attack.LengthWeights.Where(pair => pair.Key is >= 2 and <= 4 && float.IsFinite(pair.Value) && pair.Value > 0).ToList();
            if (weights.Count == 0) return null;
            var rollLength = _random.NextDouble() * weights.Sum(pair => (double)pair.Value);
            var count = weights[^1].Key;
            foreach (var weight in weights) { rollLength -= weight.Value; if (rollLength <= 0) { count = weight.Key; break; } }
            var strikes = TryComp<BossSequenceHistoryComponent>(boss, out var memory) ? memory.LastStrikes.ToList() : new();
            var result = new List<BossTelegraphPattern>();
            var directions = new[] { BossTelegraphPattern.Sinistra, BossTelegraphPattern.Dextra, BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum };
            for (var i = 0; i < count; i++)
            {
                var choices = directions.Where(direction => !attack.PreventTripleRepeats || strikes.Count < 2 ||
                    strikes[^1] != direction || strikes[^2] != direction).ToList();
                var selected = _random.Pick(choices);
                result.Add(selected); strikes.Add(selected);
            }
            return result;
        }
        var previous = TryComp<BossSequenceHistoryComponent>(boss, out var history) ? history.Last : new();
        var candidates = attack.Recipes.Where(recipe => ValidQueue(recipe.Strikes))
            .SelectMany(recipe => Enumerable.Range(0, 4).SelectMany(rotation => new[] { false, true }
                .Select(mirror => recipe.Strikes.Select(pattern => TransformDirection(pattern, rotation, mirror)).ToList())))
            .Where(queue => !queue.SequenceEqual(previous)).DistinctBy(queue => string.Join(",", queue)).ToList();
        var lengths = attack.LengthWeights.Where(pair => float.IsFinite(pair.Value) && pair.Value > 0 &&
            candidates.Any(queue => queue.Count == pair.Key)).ToList();
        var total = lengths.Sum(pair => (double)pair.Value);
        if (total <= 0)
            return null;
        var roll = _random.NextDouble() * total;
        var length = lengths[^1].Key;
        foreach (var pair in lengths)
        {
            roll -= pair.Value;
            if (roll > 0)
                continue;
            length = pair.Key;
            break;
        }
        return _random.Pick(candidates.Where(queue => queue.Count == length).ToList());
    }

    public bool TryStart(EntityUid boss, BossPreparedSequenceAttack attack, bool preview = false,
        IReadOnlyList<BossTelegraphPattern>? queue = null, float? interval = null)
    {
        var gap = interval ?? attack.StrikeInterval;
        if (!TryComp<BossComponent>(boss, out var encounter) || encounter.Finished || encounter.Health <= 0 ||
            Terminating(boss) || MetaData(boss).EntityPaused || EntityManager.System<BossActionSystem>().IsBusy(boss) || !HasComp<BossTelegraphSettingsComponent>(boss) ||
            !_telegraphs.TryGetArena(boss, out var arena) ||
            (!preview && (!encounter.Active || _telegraphs.LivingPlayers(boss).Count == 0)) ||
            !ValidDuration(attack.PreloadTime) || !ValidDuration(attack.ReleaseTime) || !ValidDuration(attack.WarningLeadTime) ||
            !float.IsFinite(gap) || gap < 1.6f || gap > 10f ||
            !float.IsFinite(attack.Cooldown) || attack.Cooldown < 0 ||
            (!preview && (attack.Damage.Empty || attack.Damage.DamageDict.Values.Any(value => value <= 0))))
            return false;
        var selected = queue?.ToList() ?? PickQueue(boss, attack);
        if (selected == null || !ValidQueue(selected))
            return false;
        if (!preview && attack.PreventTripleRepeats && !CanFollowHistory(boss, selected)) return false;
        var sequence = AddComp<BossSequenceComponent>(boss);
        sequence.Attack = attack;
        sequence.Queue = selected;
        sequence.Arena = arena.Owner;
        sequence.Grid = arena.Comp.CombatGrid;
        var rotation = _transform.GetWorldRotation(boss) - _transform.GetWorldRotation(sequence.Grid);
        sequence.Facing = Comp<BossTelegraphSettingsComponent>(boss).FixedCardinals ? 0 :
            ((int)Math.Round(rotation.Degrees / 90) % 4 + 4) % 4;
        sequence.Preview = preview;
        sequence.Interval = gap;
        ShowPreparation(boss, sequence);
        return true;
    }

    private static bool ValidDuration(float seconds) => float.IsFinite(seconds) && seconds > 0 && seconds <= 120;

    public bool Cancel(EntityUid boss)
    {
        if (!HasComp<BossSequenceComponent>(boss))
            return false;
        RemComp<BossSequenceComponent>(boss);
        return true;
    }

    private void OnShutdown(EntityUid boss, BossSequenceComponent sequence, ComponentShutdown args)
    {
        _telegraphs.Clear(boss);
        if (Terminating(boss))
            return;
        if (TryComp<BossCastBarComponent>(boss, out var bar))
        {
            bar.Casting = false;
            bar.ClearCastName();
            bar.StartTime = bar.EndTime = TimeSpan.Zero;
            Dirty(boss, bar);
        }
        EntityManager.System<BossSystem>().FinishBossAction(boss, sequence.Attack, sequence.Completed && !sequence.Preview);
    }

    private void SetBar(EntityUid boss, BossSequenceComponent sequence, string name, float seconds)
    {
        sequence.PhaseStart = _timing.CurTime;
        sequence.Deadline = sequence.PhaseStart + TimeSpan.FromSeconds(seconds);
        var bar = EnsureComp<BossCastBarComponent>(boss);
        bar.Casting = true;
        bar.Preparing = sequence.Phase == BossSequencePhase.Preparing;
        bar.Visible = sequence.Phase != BossSequencePhase.Resolving;
        bar.Anchor = null;
        if (bar.Visible) bar.SetCastName(name);
        else bar.ClearCastName();
        bar.StartTime = sequence.PhaseStart;
        bar.EndTime = sequence.Deadline;
        Dirty(boss, bar);
    }

    private void ShowPreparation(EntityUid boss, BossSequenceComponent sequence)
    {
        sequence.Phase = BossSequencePhase.Preparing;                            // show the directions before releasing the strikes
        var name = EntityManager.System<BossCastNameSystem>().CastNameKey(boss, sequence.Queue[sequence.Index], sentencing: true);
        SetBar(boss, sequence, name, sequence.Attack.PreloadTime);
        _telegraphs.PrepareSequence(boss, sequence.Queue[sequence.Index], sequence.Facing, true);
    }

    private void ShowRelease(EntityUid boss, BossSequenceComponent sequence)
    {
        sequence.Phase = BossSequencePhase.Releasing;
        sequence.Index = 0;                                                      // replay stored directions from the beginning
        SetBar(boss, sequence, sequence.Attack.ReleaseName, sequence.Attack.ReleaseTime);
        _telegraphs.PrepareSequence(boss, sequence.Queue[0], sequence.Facing, false);
    }

    private void Resolve(EntityUid boss, BossSequenceComponent sequence)
    {
        var danger = _telegraphs.SnapshotDanger(boss).ToHashSet();              // hit the same tiles the warning shows

        var victims = EntityManager.AllEntities<MobStateComponent>().Where(entity =>
            _telegraphs.TryGetPlayerTile(boss, entity.Owner, out var tile, allowCritical: true) && danger.Contains(tile))
            .Select(entity => entity.Owner).ToArray();
        sequence.StrikesResolved++;
        sequence.LastImpact = _timing.CurTime;
        if (!sequence.Preview)
        {
            RecordStrike(boss, sequence.Queue[sequence.Index]);                 // only remember strikes that actually went off
            _telegraphs.ShowImpact(boss, danger, sequence.Attack.ImpactEffect);
            foreach (var victim in victims)
            {
                if (!HasComp<BossSequenceComponent>(boss) || !TryComp<BossComponent>(boss, out var encounter) ||
                    encounter.Finished || !encounter.Active)
                    return;
                _damage.TryChangeDamage(victim, sequence.Attack.Damage, origin: boss);
            }
        }
        if (sequence.Preview)
            _telegraphs.ShowCleavePreviewImpact(boss);
        if (!HasComp<BossSequenceComponent>(boss))
            return;
        _telegraphs.Clear(boss);
        if (++sequence.Index >= sequence.Queue.Count)
        {
            EnsureComp<BossSequenceHistoryComponent>(boss).Last = sequence.Queue.ToList();
            sequence.Completed = true;
            RemComp<BossSequenceComponent>(boss);
            return;
        }
        sequence.Phase = BossSequencePhase.Resolving;                            // wait between stored hits
        SetBar(boss, sequence, string.Empty, sequence.Interval);
        _telegraphs.PrepareSequence(boss, sequence.Queue[sequence.Index], sequence.Facing, false);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BossSequenceComponent>();
        while (query.MoveNext(out var boss, out var sequence))
        {
            if (MetaData(boss).EntityPaused)
                continue;
            if (!TryComp<BossComponent>(boss, out var encounter) || encounter.Finished || encounter.Health <= 0 ||
                !_telegraphs.TryGetArena(boss, out var arena) || arena.Owner != sequence.Arena || arena.Comp.CombatGrid != sequence.Grid ||
                (!sequence.Preview && (!encounter.Active || _telegraphs.LivingPlayers(boss).Count == 0)) ||
                _telegraphs.GetTelegraph(boss) == null)
            {
                Cancel(boss);
                continue;
            }
            if (_timing.CurTime < sequence.Deadline)
                continue;
            if (sequence.Phase == BossSequencePhase.Preparing)
            {
                if (++sequence.Index < sequence.Queue.Count)
                    ShowPreparation(boss, sequence);
                else
                    ShowRelease(boss, sequence);
            }
            else
                Resolve(boss, sequence);
        }
    }
}
