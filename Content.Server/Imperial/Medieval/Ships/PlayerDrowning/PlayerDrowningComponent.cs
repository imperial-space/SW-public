namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class PlayerDrowningComponent : Component
{
    [DataField("drownTime")]
    public int DrownTime;

    [DataField("speedModifier")]
    public float SpeedModifier = 0.5f;

    [DataField("Undrowable")]
    public bool Undrowable;
}
