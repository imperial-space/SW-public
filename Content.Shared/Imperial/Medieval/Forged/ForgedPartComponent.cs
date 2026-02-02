namespace Content.Shared.Imperial.Medieval.Forged;

[RegisterComponent]
public sealed partial class ForgedPartComponent : Component
{
    /// <summary>
    /// В какой слот на каркасе вставляется эта часть.
    /// Например: "head", "l_arm", "r_arm", "leg".
    /// </summary>
    [DataField("partSlot"), ViewVariables(VVAccess.ReadWrite)]
    public string PartSlot = "head";

    [DataField("layerState"), ViewVariables(VVAccess.ReadWrite)]
    public string LayerState = string.Empty;

    /// <summary>
    /// Будет ли эта деталь давать особые способности (на будущее).
    /// </summary>
    [DataField("abilityId")]
    public string? AbilityId;
}
