namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent, Access(typeof(BlackSpireSpawnRuleSystem))]
public sealed partial class BlackSpireSpawnMarkerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Used;
}
