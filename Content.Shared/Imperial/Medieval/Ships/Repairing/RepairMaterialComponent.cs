namespace Content.Shared.Imperial.Medieval.Ships.Repairing;

/// <summary>
/// Marks a stack as a valid hull repair material.
/// </summary>
[RegisterComponent]
public sealed partial class RepairMaterialComponent : Component
{
    [DataField("matType")]
    public string MatType = string.Empty;
}
