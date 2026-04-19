using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipDrowningComponent : Component
{
    [DataField("DrownLevel"), AutoNetworkedField]
    public int DrownLevel;

    [DataField("DrownMaxLevel"), AutoNetworkedField]
    public float DrownMaxLevel;

    [DataField("floodPerDamageStage")]
    public int FloodPerDamageStage = 10;

    [DataField("passiveDrainPerTick")]
    public int PassiveDrainPerTick = 5;

    [DataField("passiveRisePerTick")]
    public int PassiveRisePerTick = 5;

    [DataField("maxFloodPerTile")]
    public int MaxFloodPerTile = 100;

    public float VisualDrownLevel;
    public Vector2 VisualWaterOffset;
    public bool VisualDataInitialized;
}
