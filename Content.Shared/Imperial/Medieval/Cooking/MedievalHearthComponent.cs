using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

[RegisterComponent]
public sealed partial class MedievalHearthComponent : Component
{
    // all times are in seconds
    [DataField]
    public float BurnTime;

    [DataField]
    public int LogSlots = 4;

    [DataField]
    public float SlotBurnTime = 360f;

    [DataField]
    public float DyingBurnTime = 120f;

    // exact prototype match, parents don't count
    [DataField]
    public Dictionary<EntProtoId, float> Fuels = new();

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, float> StackFuels = new();

    [DataField]
    public SoundSpecifier? FuelSound;

    [DataField]
    public Color OutColor = Color.FromHex("#8a8178");

    [DataField]
    public Color DyingColor = Color.FromHex("#c9a458");

    [DataField]
    public Color LowColor = Color.FromHex("#e0b24a");

    [DataField]
    public Color WarmColor = Color.FromHex("#e8923a");

    [DataField]
    public Color HotColor = Color.FromHex("#d8402a");

    [ViewVariables]
    public MedievalHearthState State;

    public float TickTimer;

    public int VisualLogs = -1;
}
