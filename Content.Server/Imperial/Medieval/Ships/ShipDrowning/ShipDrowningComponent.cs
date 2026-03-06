namespace Content.Server.Imperial.Medieval.Ships.ShipDrowning;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class ShipDrowningComponent : Component
{
    [DataField("DrownLevel")]
    public int DrownLevel;

    [DataField("DrownMaxLevel")]
    public float DrownMaxLevel = 10000000000;
}
