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
    public float StepsSpawnSpeed = 1.5f;

    [DataField]
    public float NothingTime = 1f;

    [DataField]
    public float ExcellentTime = 0.5f;

    [DataField]
    public float PenaltyActivatorChance = 0.3f;

    #endregion

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool ReadyToForge = false;
}

