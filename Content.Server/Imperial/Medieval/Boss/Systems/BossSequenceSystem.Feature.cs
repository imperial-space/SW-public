using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSequenceSystem
{
    string IBossActionFeature.Id => "stored-cleaves";
    bool IBossActionFeature.IsBusy(EntityUid boss) => HasComp<BossSequenceComponent>(boss);
    bool IBossActionFeature.Cancel(EntityUid boss) => Cancel(boss);
    bool IBossActionFeature.Supports(BossAttack attack) => attack is BossPreparedSequenceAttack;
    bool IBossActionFeature.Start(EntityUid boss, BossAttack attack, IEnumerable<EntityUid> targets)
        => attack is BossPreparedSequenceAttack sequence && TryStart(boss, sequence);
    bool IBossActionFeature.Supports(BossEncounterStep step) => step.Combo;
    bool IBossActionFeature.Start(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step, DamageSpecifier damage)
    {
        var configured = EntityManager.System<BossMechanicDefinitionSystem>().Attack<BossPreparedSequenceAttack>(boss) ??
            Comp<BossComponent>(boss).Stages.Values.SelectMany(stage => stage.Attacks)
            .OfType<BossPreparedSequenceAttack>().FirstOrDefault();
        if (configured == null) return false;
        var attack = new BossPreparedSequenceAttack
        {
            Damage = damage,
            PreloadTime = step.SequencePreloadTime ?? configured.PreloadTime,
            ReleaseTime = step.SequenceReleaseTime ?? configured.ReleaseTime,
            StrikeInterval = step.SequenceStrikeInterval ?? configured.StrikeInterval,
            ReleaseName = configured.ReleaseName,
            WarningLeadTime = configured.WarningLeadTime,
            LengthWeights = step.SequenceLengthWeights ?? configured.LengthWeights,
            RandomDirections = true,
            PreventTripleRepeats = true,
        };
        IReadOnlyList<BossTelegraphPattern>? queue = step.Queue.Count == 0 ? null : step.Queue;
        if (queue != null && !CanFollowHistory(boss, queue))
            queue = new[] { (0, true), (1, false), (2, false), (3, false) }
                .Select(change => step.Queue.Select(direction => TransformDirection(direction, change.Item1, change.Item2)).ToList())
                .FirstOrDefault(candidate => CanFollowHistory(boss, candidate));
        return TryStart(boss, attack, queue: queue);
    }
}
