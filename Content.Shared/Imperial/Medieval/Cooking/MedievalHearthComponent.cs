using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - a fire that burns on wood. Click it with fuel to keep it going; when the fuel runs
///     out it goes cold and everything cooking on it pauses.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalHearthComponent : Component
{
    /// <summary>
    ///     Seconds of fuel left. A map-placed hearth with a non-zero value starts lit.
    /// </summary>
    [DataField]
    public float BurnTime;

    /// <summary>
    ///     How many logs fit in the fire. Each one is drawn on its own.
    /// </summary>
    [DataField]
    public int LogSlots = 4;

    /// <summary>
    ///     Seconds of burning one drawn log stands for. The fuel cap is <see cref="LogSlots"/> times this.
    /// </summary>
    [DataField]
    public float SlotBurnTime = 360f;

    /// <summary>
    ///     At or below this many seconds left the fire is shown as dying.
    /// </summary>
    [DataField]
    public float DyingBurnTime = 120f;

    /// <summary>
    ///     Seconds of burning one item of this entity prototype adds. Exact prototype match, so a steel-cap log
    ///     is not fuel just because a tower-cap log is.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, float> Fuels = new();

    /// <summary>
    ///     Seconds of burning one unit of this stack adds.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, float> StackFuels = new();

    [DataField]
    public SoundSpecifier? FuelSound;

    [ViewVariables]
    public MedievalHearthState State;

    public float TickTimer;

    /// <summary>
    ///     Logs last sent to the appearance, so it is only sent when it changes.
    /// </summary>
    public int VisualLogs = -1;
}
