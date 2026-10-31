namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossArenaAmmoComponent : Component
{
    public EntityUid Arena;
    public bool Fired;
    [AutoPausedField] public TimeSpan? Expires;
}
