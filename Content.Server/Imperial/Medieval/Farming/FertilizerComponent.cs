namespace Content.Shared.Imperial.Medieval.Farming;

/// <summary>
/// Fertilizer component that makes an entity fertilize soil when applied to it.
/// </summary>
[RegisterComponent]
public sealed partial class FertilizerComponent: Component
{
    [DataField]
    public float NutrientValue;
}
