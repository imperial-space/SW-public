using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftFPVPilotComponent : Component
{
    [DataField]
    public EntityUid? Drone;

}

