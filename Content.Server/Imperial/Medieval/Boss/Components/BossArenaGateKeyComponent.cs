namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossArenaGateKeyComponent : Component
{
    [DataField] public string ArenaId = string.Empty;
}
