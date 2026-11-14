using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - the bowl a served dish was ladled into, so the dish keeps looking like that bowl.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class MedievalDishBowlComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Bowl;
}
