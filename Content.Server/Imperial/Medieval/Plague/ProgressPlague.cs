using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class ProgressPlague : EntityEffect
{

    [DataField(required: true)]
    public float ProgressAmount;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var plague = args.EntityManager.System<MedievalPlagueSystem>();

        var amount = ProgressAmount;
        if (args is EntityEffectReagentArgs reagentArgs)
        {
            amount *= (float)reagentArgs.Quantity;
            if (reagentArgs.Reagent != null && reagentArgs.Reagent.ID != plague.CurrentCure)
                return;
        }

        plague.TryProgressInfection(args.TargetEntity, amount);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-progress-plague", ("chance", Probability), ("progress", ProgressAmount));
}
