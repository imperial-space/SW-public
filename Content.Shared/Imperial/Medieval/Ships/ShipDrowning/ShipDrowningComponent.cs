using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipDrowningComponent : Component
{
    /// <summary>
    /// Уровень затоплености
    /// </summary>
    [DataField("DrownLevel"), AutoNetworkedField]
    public int DrownLevel;
    /// <summary>
    /// Максимальный уровень затоплености
    /// </summary>
    [DataField("DrownMaxLevel"), AutoNetworkedField]
    public float DrownMaxLevel = 10000000000;

    public float VisualDrownLevel;
    public Vector2 VisualWaterOffset;
    public bool VisualDataInitialized;
}
