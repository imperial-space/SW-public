using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise.CombatStamina;

/// <summary>Refundable action expenditure. These points are not available stamina.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CombatStaminaComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public float Pending;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan RecoveryStarts;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastUpdate;

    [DataField, AutoNetworkedField]
    public float LightRefund = 0.80f;

    [DataField, AutoNetworkedField]
    public float MediumRefund = 0.60f;

    [DataField, AutoNetworkedField]
    public float HeavyRefund = 0.40f;

    [DataField, AutoNetworkedField]
    public TimeSpan RecoveryDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public float RecoveryPerSecond = 2f;

    [DataField, AutoNetworkedField]
    public float SprintMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public float PoiseBreakRefund = 0.5f;
}
