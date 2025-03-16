using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Customization;

[Prototype("customization")]
public sealed class CustomizationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    [DataField]
    public string Holder = string.Empty;

    [DataField]
    public Dictionary<EntProtoId, List<EntProtoId>> Map = [];
}
