using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MobRiding;

[RegisterComponent, NetworkedComponent]
public sealed partial class HorseControlComponent : Component
{
    [DataField] public float TurnSpeed = 180f;
    [DataField] public float TurnSpeedSlowdown = 0.1f;
    [DataField] public float BackwardsModifier = 0.6f;
}
