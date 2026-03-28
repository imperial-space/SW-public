namespace Content.Shared.Imperial.Medieval.Ships.Repairing;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class RepairMaterialComponent : Component
{
    [DataField("matType")]
    public string MatType;

    [DataField]
    public Vector2i TileCord;
}
