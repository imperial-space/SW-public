using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Salary;

/// <summary>
/// Lets the owner manage the salaries of their faction from the faction menu.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalSalaryManagerComponent : Component
{
    /// <summary>
    /// The treasury holds the default income of all active salaried faction members plus this share.
    /// </summary>
    public const float TreasuryMargin = 0.1f;

    public const float MinModifier = 0.5f;

    public const float MaxModifier = 2f;

    public static int GetMin(int defaultPayment) => (int) MathF.Ceiling(defaultPayment * MinModifier);

    public static int GetMax(int defaultPayment) => (int) MathF.Floor(defaultPayment * MaxModifier);
}
