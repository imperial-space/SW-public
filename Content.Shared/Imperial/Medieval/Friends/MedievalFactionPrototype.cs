using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Friends.Prototypes;

[Prototype]
public sealed class MedievalFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField]
    public bool ShowKnown = true;

    [DataField]
    public bool AllowHeadhunt = true;

    [DataField]
    public SpriteSpecifier? Icon = null;

    [DataField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, string> KnownFactions = new();
}
