using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPlagueGhostComponent : Component
{
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Points = 5;

    [ViewVariables(VVAccess.ReadWrite)]
    public int FreeInfections = 3;

    public List<EntityUid> Actions = new();
}
