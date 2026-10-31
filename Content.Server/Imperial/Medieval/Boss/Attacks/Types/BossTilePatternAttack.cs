using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using System.Linq;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTilePatternAttack : BossAttack
{
    [DataField] public DamageSpecifier Damage = new();
    [DataField] public float FollowupDelay = 2;
    [DataField] public bool SequentialConstellation;
    [DataField] public string ImpactEffect = string.Empty;
    [DataField] public int SpreadTargets = 4;

    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        if (Telegraph is not (BossTelegraphPattern.Spread or BossTelegraphPattern.Stack))
            return new[] { boss };
        var candidates = targets.Distinct().ToList();
        if (candidates.Count > 1 && entMan.TryGetComponent<BossFocusMarkerComponent>(boss, out var focus) && focus.Tank is { } tank)
            candidates.Remove(tank);
        var count = Telegraph == BossTelegraphPattern.Spread ? Math.Clamp(SpreadTargets, 1, 4) : 1;
        if (entMan.TryGetComponent<BossEncounterComponent>(boss, out var encounter))
        {
            var opposite = Telegraph == BossTelegraphPattern.Spread ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread;
            if (encounter.StoredTargets.TryGetValue(opposite, out var assigned))
                candidates.RemoveAll(assigned.Targets.Contains);
            if (Telegraph == BossTelegraphPattern.Spread && candidates.Count > 1 &&
                !encounter.StoredTargets.ContainsKey(BossTelegraphPattern.Stack) &&
                encounter.PendingSteps.Skip(encounter.PendingIndex + 1).Any(step =>
                    step.PreloadOnly && step.Pattern == BossTelegraphPattern.Stack))
                count = Math.Min(count, candidates.Count - 1);
        }
        return new PlayerTargetedAttack { TargetLimits = (count, count + 1) }
            .PickTargets(boss, candidates, entMan);
    }
}
