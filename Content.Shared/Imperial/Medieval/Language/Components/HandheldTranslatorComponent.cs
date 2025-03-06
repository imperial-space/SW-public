using Content.Shared.Actions;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Language;

/// <summary>
/// Компонент, дающий знание языков, когда находится в руках и активен
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class HandheldTranslatorComponent : Component
{
    /// <summary>
    /// Список языков, которые даёт переводчик. Писать в компонентах как:
    /// Прототип: Understand/BadSpeak/Speak
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("languages", required: true), AutoNetworkedField]
    public Dictionary<string, LanguageKnowledge> Languages;

    /// <summary>
    /// Будет ли переключаться переводчик по взаимодействию с ним
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("toggle")]
    public bool ToggleOnInteract = true;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    [DataField]
    public bool Enabled = false;

    public EntityUid? User;
}
