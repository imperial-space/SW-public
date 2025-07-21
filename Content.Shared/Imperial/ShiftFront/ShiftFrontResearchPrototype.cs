using Robust.Shared.Prototypes;

namespace Content.Shared.ShiftFrontResearch;

[Prototype("ShiftFrontResearch")]
public sealed class ShiftFrontResearchPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> RequiredResearches = new();

    [DataField]
    public List<string> BannedResearches = new();

    [DataField]
    public int Tier = 0;

    [DataField]
    public int Price = 0;

    [DataField]
    public string ResearchName = "";

    [DataField]
    public string UnicForFaction = "";
}
