namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class PlagueDizzinessComponent : Component
{
    [DataField]
    public TimeSpan EndTime = TimeSpan.Zero;
}
