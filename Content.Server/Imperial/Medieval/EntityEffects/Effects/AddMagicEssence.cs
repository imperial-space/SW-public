using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.EntityEffects;


public sealed partial class AddMagicEssence : EntityEffect
{
    private IRobustRandom? _random;
    private BindStoreOnEquipSystem? _grimoireSystem;

    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> AddedEssences = [];

    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> BonusEssences = [];

    [DataField]
    public float EssenceAddProbability;

    [DataField]
    public float BonusAddProbability;


    public override void Effect(EntityEffectBaseArgs args)
    {
        _random ??= IoCManager.Resolve<IRobustRandom>();
        _grimoireSystem ??= args.EntityManager.System<BindStoreOnEquipSystem>();

        if (args is not MagicEntityEffectsArgs magicEntityEffectsArgs)
            return;

        if (_random.Prob(EssenceAddProbability))
            _grimoireSystem.TryAddCurrency(magicEntityEffectsArgs.Performer, AddedEssences);

        if (_random.Prob(BonusAddProbability))
            _grimoireSystem.TryAddBonus(magicEntityEffectsArgs.Performer, BonusEssences);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";
}
