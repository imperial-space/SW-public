namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaFallingComponent : Component
{
    public EntityUid Arena;
    [AutoPausedField]
    public TimeSpan ReturnAt;
}
