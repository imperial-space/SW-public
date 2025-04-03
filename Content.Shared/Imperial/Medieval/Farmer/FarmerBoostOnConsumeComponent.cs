using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Farmer;

[RegisterComponent, NetworkedComponent]
public sealed partial class FarmerBoostOnConsumeComponent : Component
{
    [DataField]
    public float Time = 5f;
}
