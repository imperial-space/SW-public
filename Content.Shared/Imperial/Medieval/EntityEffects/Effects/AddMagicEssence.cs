using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.EntityEffects;

/// <summary>
/// </summary>
public sealed partial class AddMagicEssence : EntityEffectBase<AddMagicEssence>
{
    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> AddedEssences = [];

    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> BonusEssences = [];

    [DataField]
    public float EssenceAddProbability;

    [DataField]
    public float BonusAddProbability;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";
}
