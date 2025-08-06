using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Plague;

[Prototype("plagueSymptom")]
public sealed partial class MedievalPlagueSymptomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Name { get; } = default!;

    [DataField(required: true)]
    public string Desc { get; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; } = default!;

    [DataField(required: true)]
    public int Cost { get; } = 5;

    [DataField(required: true)]
    public int Tier { get; } = 1;

    [DataField]
    public BaseSymptomEvent? TargetEvent { get; }

    [DataField]
    public EntProtoId[] Actions { get; } = Array.Empty<EntProtoId>();
}
