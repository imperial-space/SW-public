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
    public float MediumRefund = 0.65f;

    [DataField, AutoNetworkedField]
    public float HeavyRefund = 0.50f;

    [DataField, AutoNetworkedField]
    public TimeSpan RecoveryDelay = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public float RecoveryPerSecond = 3f;

    [DataField, AutoNetworkedField]
    public float SprintMultiplier = 1.8f;

    [DataField, AutoNetworkedField]
    public float PoiseBreakRefund = 0.5f;
}
