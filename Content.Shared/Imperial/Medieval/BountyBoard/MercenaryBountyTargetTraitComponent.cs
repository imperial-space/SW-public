using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.BountyBoard;

[RegisterComponent, NetworkedComponent]
public sealed partial class MercenaryBountyTargetTraitComponent : Component
{
    [DataField]
    public EntProtoId<StackComponent> CurrencyProtoId { get; set; } = "MedievalRevent70";
}
