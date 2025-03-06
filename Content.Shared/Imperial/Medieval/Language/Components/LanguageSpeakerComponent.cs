using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Language;

/// <summary>
/// Данный компонент позволяет сущности понимать и говорить на языках
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LanguageSpeakerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public string? CurrentLanguage = default!;

    /// <summary>
    /// Список языков, которые знает сущность. Писать в компонентах как:
    /// Прототип: Understand/BadSpeak/Speak
    /// </summary>
    [DataField("languages"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public Dictionary<string, LanguageKnowledge> Languages = new();
}

/// <summary>
/// Уровень владения языком.
/// <see cref="Understand"/> позволяет сущности понимать язык, но не говорить на нём
/// <see cref="BadSpeak"/> позволяет сущности говорить на языке, однако при этом подпарчивает текст. Как эффект от алкоголя, но без рыганий
/// <see cref="Speak"/> позволяет сущности свободно говорить на языке
/// </summary>
[Serializable, NetSerializable]
public enum LanguageKnowledge : int
{
    Understand = 0,
    BadSpeak = 1,
    Speak = 2
}
