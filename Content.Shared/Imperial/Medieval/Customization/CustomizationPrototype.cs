using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Customization;

[Prototype("customization")]
public sealed partial class CustomizationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Holder = string.Empty;

    [DataField]
    public Dictionary<EntProtoId, List<EntProtoId>> Map = [];
}
