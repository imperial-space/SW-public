using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTraversalSystem
{
    partial void ApplyStepParameters(BossEncounterStep step, ref bool valid);
    string IBossActionFeature.Id => "traversal";
    bool IBossActionFeature.IsBusy(EntityUid boss) => HasComp<BossTraversalComponent>(boss);
    bool IBossActionFeature.BlocksTankAttack(EntityUid boss)
        => TryComp<BossTraversalComponent>(boss, out var traversal) &&
           !(traversal.Kind == BossTraversalKind.CrystalRain && traversal.Phase == BossTraversalPhase.Rain);
    bool IBossActionFeature.Cancel(EntityUid boss) => Cancel(boss);
    bool IBossActionFeature.Supports(BossEncounterStep step) => step.Traversal != null;
    bool IBossActionFeature.Start(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step, DamageSpecifier damage)
    {
        if (step.Traversal is not { } kind) return false;
        var valid = true;
        ApplyStepParameters(step, ref valid);
        if (!valid) return false;                                               // bad module settings skip this step safely
        encounter.Damage.TryGetValue("sentence", out var spreadDamage);
        encounter.Damage.TryGetValue("cleave", out var flankDamage);
        return TryStart(boss, kind, damage, step.ConeSpread, step.ConeFlank, spreadDamage, step.RainCount,
            direction: step.Direction, distance: step.Distance,
            rainWarningTime: step.RainWarningTime, rainSizes: step.RainSizes,
            rainCastTime: step.RainCastTime, rainConcurrent: step.RainConcurrent,
            rainSpawnInterval: step.RainSpawnInterval, rainDoubleChance: step.RainDoubleChance,
            rainMaxConcurrent: step.RainMaxConcurrent, rainMinSpawnInterval: step.RainMinSpawnInterval,
            rainRampSeconds: step.RainRampSeconds, flankDamage: step.ConeFlank ? flankDamage : null);
    }
}
