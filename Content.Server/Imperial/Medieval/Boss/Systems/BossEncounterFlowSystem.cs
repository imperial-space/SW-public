using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossEncounterFlowSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public bool TryPrepare(BossEncounterComponent encounter)
    {
        if (encounter.FightRunning) return false;                                // a running fight keeps its own settings
        if (encounter.Flow is not { } id) return true;                           // keep existing inline encounters working
        if (!_prototypes.TryIndex(id, out var flow) || !IsValid(flow)) return false;
        encounter.MechanicSet = flow.MechanicSet;
        encounter.OpeningDelay = flow.OpeningDelay;
        encounter.EntryGrace = flow.EntryGrace;
        encounter.IdleReset = flow.IdleReset;
        encounter.Recovery = flow.Recovery;
        encounter.TankInterval = flow.TankInterval;
        encounter.TankDamage = _serialization.CreateCopy(flow.TankDamage, notNullableOverride: true);
        encounter.Damage = _serialization.CreateCopy(flow.Damage, notNullableOverride: true);
        encounter.PatternDamage = _serialization.CreateCopy(flow.PatternDamage, notNullableOverride: true);
        encounter.Tutorial = _serialization.CreateCopy(flow.Tutorial, notNullableOverride: true);
        encounter.Rotation = _serialization.CreateCopy(flow.Rotation, notNullableOverride: true);
        encounter.Escalation = _serialization.CreateCopy(flow.Escalation, notNullableOverride: true);
        encounter.ActiveFlow = flow.ID;                                         // copied lists cannot change another fight or the prototype
        return true;
    }

    public static bool IsValid(BossEncounterFlowPrototype flow)
        => Delay(flow.OpeningDelay, true) && Delay(flow.EntryGrace, true) && Delay(flow.IdleReset) &&
           Delay(flow.Recovery) && Delay(flow.TankInterval) && BossMechanicSystem.IsValidDamage(flow.TankDamage) &&
           flow.Damage.Values.All(BossMechanicSystem.IsValidDamage) &&
           flow.PatternDamage.Values.All(flow.Damage.ContainsKey) &&
           flow.Tutorial.Concat(flow.Rotation).Concat(flow.Escalation).All(step => ValidStep(step));

    private static bool Delay(float value, bool allowZero = false)
        => float.IsFinite(value) && value <= 3600 && (allowZero ? value >= 0 : value > 0);

    private static bool ValidStep(BossEncounterStep step, int depth = 0)
    {
        if (depth > 8 || !float.IsFinite(step.Weight) || step.Weight < 0 ||
            !float.IsFinite(step.Chance) || step.Chance is < 0 or > 1 ||
            !BossMechanicSystem.IsValidTimings(step.CastTime, step.FollowupDelay) ||
            step.RecoveryAfter is { } recovery && !Delay(recovery, true)) return false;
        if (step.Choices.Count > 0) return step.Choices.All(choice => ValidStep(choice, depth + 1));
        if (step.SequencePreloadTime is { } preload && !Delay(preload) ||
            step.SequenceReleaseTime is { } release && !Delay(release) ||
            step.SequenceStrikeInterval is { } interval && !Delay(interval)) return false;
        if (step.SequenceLengthWeights is { } weights && (weights.Count == 0 ||
            weights.Any(pair => pair.Key is < 2 or > 4 || !float.IsFinite(pair.Value) || pair.Value <= 0))) return false;
        return step.Traversal != BossTraversalKind.CrystalRain ||
            step.RainCount > 0 && step.RainSizes.Count > 0 && step.RainSizes.All(size => size is 2 or 3) &&
            Delay(step.RainCastTime) && Delay(step.RainWarningTime) && Delay(step.RainSpawnInterval) &&
            Delay(step.RainMinSpawnInterval) && Delay(step.RainRampSeconds) &&
            step.RainConcurrent > 0 && step.RainMaxConcurrent >= step.RainConcurrent &&
            float.IsFinite(step.RainDoubleChance) && step.RainDoubleChance is >= 0 and <= 1;
    }
}
