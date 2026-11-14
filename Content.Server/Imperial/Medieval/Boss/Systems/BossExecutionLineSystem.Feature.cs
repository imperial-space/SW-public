using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossExecutionLineSystem
{
    string IBossActionFeature.Id => "execution";
    void IBossActionFeature.Cleanup(EntityUid boss) => Clear(boss);
    bool IBossActionFeature.Supports(BossEncounterStep step) => step.TankBuster;
    bool IBossActionFeature.Start(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step, DamageSpecifier damage)
    {
        if (!TryComp<BossFocusMarkerComponent>(boss, out var focus) || focus.Tank is not { } tank ||
            !EntityManager.System<BossTelegraphSystem>().TryGetPlayerTile(boss, tank, out _)) return false;
        return EntityManager.System<BossSystem>().TryStartBossCast(boss, new BossTankBusterAttack
        {
            CastTime = step.CastTime,
            CastName = EntityManager.System<BossCastNameSystem>().NameKey(boss, "Execution"),
            Actions = new() { new BossTankBusterImpact { Damage = damage } },
        }, new[] { tank });
    }
    bool IBossActionFeature.PrepareCast(EntityUid boss, BossCastingComponent cast, BossCastBarComponent bar)
    {
        if (cast.Attack is not BossTankBusterAttack || !TryComp<BossTelegraphSettingsComponent>(boss, out var settings)) return true;
        if (settings.ExecutionLine || EntityManager.System<BossMechanicDefinitionSystem>().Slot(boss, BossActionSlot.Execution) != null)
        {
            cast.ExecutionLine = Start(boss, cast.Targets[0], bar.StartTime, bar.EndTime);
            return cast.ExecutionLine != null;
        }
        cast.TargetMarker = EntityManager.System<BossTelegraphSystem>().CreateMarker(boss, cast.Targets[0], BossActorMarkerKind.TankBuster);
        return true;
    }
    void IBossActionFeature.StopCast(EntityUid boss, BossCastingComponent cast)
    {
        if (cast.Attack is not BossTankBusterAttack || cast.Resolving) return;
        Cancel(cast.ExecutionLine);
        EntityManager.System<BossEncounterSystem>().StepFinished(boss, false);
    }
    void IBossActionFeature.FinishCast(EntityUid boss, BossCastingComponent cast, bool completed)
    {
        if (cast.Attack is not BossTankBusterAttack) return;
        if (cast.ExecutionLine is { } line && TryComp<BossExecutionLineComponent>(line, out var execution) && !execution.Resolved)
            Cancel(line);
        EntityManager.System<BossEncounterSystem>().StepFinished(boss, completed);
    }
}
