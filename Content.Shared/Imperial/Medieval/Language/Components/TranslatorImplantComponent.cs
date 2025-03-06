using Content.Shared.Actions;
using Content.Shared.ADT.Language;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Implants.Components;

/// <summary>
/// Данный компонент даёт знание языков, когда его обладатель является имплантом и имплантирован в кого-либо
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TranslatorImplantComponent : Component
{
    /// <summary>
    /// Список языков, которые добавляет имплант. Писать в компонентах как:
    /// Прототип: Understand/BadSpeak/Speak
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("languages")]
    public Dictionary<string, LanguageKnowledge> Languages = new();

    /// <summary>
    /// The entity this implant is inside
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ImplantedEntity;

    /// <summary>
    /// Should this implant be removeable?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("permanent"), AutoNetworkedField]
    public bool Permanent = false;
}
