namespace Content.Server.Imperial.Medieval.WarningOnAttach;

[RegisterComponent]
public sealed partial class WarningOnAttachComponent : Component
{
    [DataField]
    public string Message = "";

    public List<string> Players = new();
}
