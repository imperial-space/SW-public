namespace Content.Server.Imperial.Medieval.Cult.Bloodspells.mateials;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class BloodMaterialComponent : Component
{
    [DataField("materialType")]
    public string MaterialType { get; set; } = string.Empty;
}
