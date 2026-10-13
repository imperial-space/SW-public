namespace Content.Shared.Imperial.Medieval.Nutrition;

/// <summary>
///     imperial medieval - multiplies how fast the entity gets hungry, on top of its own rate. Applied once, when the component starts.
/// </summary>
[RegisterComponent]
public sealed partial class HungerRateModifierComponent : Component
{
    [DataField]
    public float Modifier = 1f;
}

/// <summary>
///     imperial medieval - multiplies how fast the entity gets thirsty, on top of its own rate. Applied once, when the component starts.
/// </summary>
[RegisterComponent]
public sealed partial class ThirstRateModifierComponent : Component
{
    [DataField]
    public float Modifier = 1f;
}
