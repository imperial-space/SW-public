namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class PlayerDrowningComponent : Component
{
    [DataField("drownTime")]
    public int DrownTime = 15;
}
