using Content.Shared.Fishing.Enums;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingBaitComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public FishingBaitType BaitType = FishingBaitType.Plant;
}
