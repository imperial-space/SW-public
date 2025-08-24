using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class ProgressPlague : EntityEffect
{

    [DataField(required: true)]
    public float ProgressAmount;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entMan = args.EntityManager;

        var amount = ProgressAmount;
        if (args is EntityEffectReagentArgs reagentArgs)
            amount *= (float)reagentArgs.Quantity;

        entMan.System<MedievalPlagueSystem>().TryProgressInfection(args.TargetEntity, amount);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-progress-plague", ("chance", Probability), ("progress", ProgressAmount));
}
