using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class MedievalDishBowlComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Bowl;

    [DataField]
    public string Solution = "food";
}
