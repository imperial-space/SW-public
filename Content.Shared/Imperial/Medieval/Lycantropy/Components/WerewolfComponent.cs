using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WerewolfComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? InfectAction;

    public bool InfectOn = false;

    public bool TearingOn = false;

    [AutoNetworkedField]
    public Dictionary<string, EntityUid> Actions = new();
}
