using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Plague;

[Prototype("plagueSymptom")]
public sealed partial class MedievalPlagueSymptomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField(required: true)]
    public string Desc { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;

    [DataField(required: true)]
    public int Cost { get; private set; } = 5;

    [DataField(required: true)]
    public int Tier { get; private set; } = 1;

    [DataField(required: true)]
    public Vector2 Position;

    [DataField]
    public bool StaticCost = false;

    [DataField]
    public string[] Required = Array.Empty<string>();

    [DataField(serverOnly: true)]
    public object? IncubationTargetEvent { get; private set; }

    [DataField(serverOnly: true)]
    public object? TargetEvent { get; private set; }

    [DataField(serverOnly: true)]
    public object? BroadcastEvent { get; private set; }

    [DataField(serverOnly: true)]
    public EntProtoId[] Actions { get; private set; } = Array.Empty<EntProtoId>();

    [DataField]
    public SymptomCategory Category = SymptomCategory.Symptom;

    public int GetCost(SummaryPlagueData data)
    {
        if (StaticCost)
            return Cost;

        return Cost * data.PlagueGhosts;
    }
}

public enum SymptomCategory
{
    Action,
    Symptom,
    Spread
}
