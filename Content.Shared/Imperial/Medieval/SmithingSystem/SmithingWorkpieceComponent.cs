using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SmithingWorkpieceComponent : Component
{
    #region Setup

    [DataField]
    public EntProtoId FinalProductEntity { get; set; }

    [DataField]
    public int Steps = 20;

    [DataField]
    public float StepsSpawnSpeed = 1.1f;

    [DataField]
    public float NothingTime = 0.7f;

    [DataField]
    public float ExcellentTime = 0.4f;

    [DataField]
    public float PenaltyActivatorChance = 0.35f;

    #endregion

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool ReadyToForge = false;
}

