namespace Content.Server.Imperial.Medieval.SoulboundTrigger;

[RegisterComponent]
public sealed partial class SoulboundTriggerComponent : Component
{
    [ViewVariables]
    public EntityUid? User;

    [DataField]
    public string? KeyOut = "soulboundtrigger";
}
