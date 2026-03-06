using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class ShipDrowningComponent : Component
{
    /// <summary>
    /// Уровень затоплености
    /// </summary>
    [DataField("DrownLevel")]
    public int DrownLevel;
    /// <summary>
    /// Максимальный уровень затоплености
    /// </summary>
    [DataField("DrownMaxLevel")]
    public float DrownMaxLevel = 10000000000;
}
