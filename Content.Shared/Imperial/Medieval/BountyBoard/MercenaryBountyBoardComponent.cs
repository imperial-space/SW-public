using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.BountyBoard;

[RegisterComponent, NetworkedComponent]
public sealed partial class MercenaryBountyBoardComponent : Component
{
    [DataField]
    public EntProtoId<MercenaryContractComponent> ContractProtoId { get; set; } = "MercContract";

    [DataField]
    public TimeSpan CooldownTime { get; set; } = TimeSpan.FromMinutes(20);

    public TimeSpan NextTimeUse { get; set; }
}
