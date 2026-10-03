using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Knowledge;

/// <summary>A learned rule, independent of the physical edition which teaches it.</summary>
[Prototype("medievalKnowledge")]
public sealed partial class MedievalKnowledgePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name = string.Empty;
    [DataField(required: true)] public LocId Description = string.Empty;
    [DataField(required: true)] public LocId BookTitle = string.Empty;
    [DataField(required: true)] public LocId BookText = string.Empty;
    [DataField(required: true)] public EntProtoId OriginalBook;
    [DataField] public int Tier = 1;
    [DataField] public SpriteSpecifier? Icon;
    [DataField] public string? Language;
    [DataField] public List<EntProtoId> Actions = new();
}
