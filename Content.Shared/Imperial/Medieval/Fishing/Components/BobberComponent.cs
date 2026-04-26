using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BobberComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Rod;
}
