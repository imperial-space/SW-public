using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPlagueGhostComponent : Component
{
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Points = 5;

    [AutoNetworkedField]
    public EntityUid? InfectAction;

    [ViewVariables(VVAccess.ReadWrite)]
    public int FreeInfections = 3;

    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<AlertPrototype> AlertId = "MedievalPlaguePoints";

    public List<EntityUid> Actions = new();
}
