using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Plague;

public sealed partial class GrantPlagueImmunity : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        // upstream need to fix
        //var plague = args.EntityManager.System<MedievalPlagueSystem>();
        //var timing = IoCManager.Resolve<IGameTiming>();
        //
        //if (args is EntityEffectReagentArgs reagentArgs)
        //{
        //    if (reagentArgs.Reagent != null && reagentArgs.Reagent.ID != plague.CurrentCure)
        //        return;
        //}
        //
        //if (args.EntityManager.HasComponent<MedievalPlagueInfectedComponent>(args.TargetEntity) ||
        //    args.EntityManager.HasComponent<MedievalPlagueImmuneComponent>(args.TargetEntity) ||
        //    !args.EntityManager.HasComponent<MedievalCanBeInfectedComponent>(args.TargetEntity))
        //    return;
        //
        //var immune = args.EntityManager.EnsureComponent<MedievalPlagueImmuneComponent>(args.TargetEntity);
        //immune.StartTime = timing.CurTime;
        //immune.HardImmunity = true;
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-plague-immunity", ("chance", Probability));
}
