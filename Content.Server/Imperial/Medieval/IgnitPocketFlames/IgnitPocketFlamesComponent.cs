namespace Content.Server.Imperial.Medieval;

[RegisterComponent]
public sealed partial class IgnitPocketFlamesComponent : Component
{
    [DataField]
    public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);
    [DataField]
    public bool Enabled = true;

    [DataField]
    public float Power = 1f;
}
