using Content.Shared.Fishing.Enums;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public float TensionAccelerationDelta = -1.5f;

    [DataField, AutoNetworkedField]
    public float TensionAccelerationDeltaPressed = 1.5f;

    [DataField, AutoNetworkedField]
    public float ProgressPerTick = 30f;

    [DataField, AutoNetworkedField]
    public FishingLocationType Location = FishingLocationType.River;

    [DataField, AutoNetworkedField]
    public FishingBaitType Bait = FishingBaitType.Plant;
}
