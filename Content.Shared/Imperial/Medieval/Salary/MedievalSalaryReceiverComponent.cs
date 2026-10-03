namespace Content.Shared.Imperial.Medieval.Salary;

/// <summary>
/// Accrues <see cref="PaymentAmount"/> into <see cref="Balance"/> every <see cref="ReceiveTimer"/>.
/// The balance is withdrawn at an ATM.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MedievalSalaryReceiverComponent : Component
{
    [DataField]
    public TimeSpan ReceiveTimer = TimeSpan.FromMinutes(20);

    [DataField]
    public int PaymentAmount;

    [DataField]
    public int Balance;

    [DataField, AutoPausedField]
    public TimeSpan NextPayment;

    /// <summary>
    /// Salaries of the same role type are changed together by the treasurer.
    /// </summary>
    [DataField]
    public string RoleType = string.Empty;

    /// <summary>
    /// Set when the treasurer gave this receiver a personal salary, so role changes skip it.
    /// </summary>
    [DataField]
    public bool PersonallyModified;
}
