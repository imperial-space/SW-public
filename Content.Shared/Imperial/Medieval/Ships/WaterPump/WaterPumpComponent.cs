using System.Runtime.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WaterPumpComponent : Component
{
    [DataField("waterCount")]
    public float WaterCount = 500f;

}
