using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ImperialVehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialVehiclePilotComponent : Component
{
    /// <summary>
    /// The vehicle this rider is currently riding.
    /// </summary>
    [ViewVariables] public EntityUid? Vehicle;

    public override bool SendOnlyToOwner => true;
}

[Serializable, NetSerializable]
public sealed class ImperialVehiclePilotComponentState : ComponentState
{
    public NetEntity? Entity;
}
