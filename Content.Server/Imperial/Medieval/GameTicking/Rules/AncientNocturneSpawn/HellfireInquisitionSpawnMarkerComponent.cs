namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent, Access(typeof(AncientNocturneSpawnRuleSystem))]
public sealed partial class HellfireInquisitionSpawnMarkerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Used;
}
