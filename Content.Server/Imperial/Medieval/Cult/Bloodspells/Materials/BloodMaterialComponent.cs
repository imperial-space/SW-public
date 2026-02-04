namespace Content.Server.Imperial.Medieval.Cult.Bloodspells.Materials;

[RegisterComponent]
public sealed partial class BloodMaterialComponent : Component
{
    [DataField("materialType")]
    public string MaterialType { get; set; } = string.Empty;
}
