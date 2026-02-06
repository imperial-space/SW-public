using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Forged;

[RegisterComponent]
public sealed partial class ForgedModuleComponent : Component
{
    /// <summary>
    /// В какой слот на каркасе вставляется эта часть.
    /// Например: "head", "l_arm", "r_arm", "leg".
    /// </summary>
    [DataField("partSlot"), ViewVariables(VVAccess.ReadWrite)]
    public string ModuleSlot = "head";

    /// <summary>
    /// Название модуля. Скопировать с state из спрайта
    /// </summary>
    [DataField("layerState"), ViewVariables(VVAccess.ReadWrite)]
    public string LayerState = string.Empty;

    /// <summary>
    /// Название модуля. Скопировать с state из спрайта
    /// </summary>
    [DataField("rsiPath"), ViewVariables(VVAccess.ReadWrite)]
    public ResPath RsiPath = ResPath.Empty;

    /// <summary>
    /// Будет ли эта деталь давать особые способности (на будущее).
    /// </summary>
    [DataField("abilityId")]
    public string? AbilityId;
}
