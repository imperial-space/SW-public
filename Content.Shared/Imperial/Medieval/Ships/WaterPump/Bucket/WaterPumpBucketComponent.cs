namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WaterPumpBucketComponent : Component
{
    [DataField("waterCount")]
    public int WaterCount = 50;
}
