using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Power;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPowerStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Powered;
}
