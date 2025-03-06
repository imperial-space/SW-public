namespace Content.Shared.ADT.Language;

/// <summary>
/// Получает все языки сущности, на которую вызывается
/// </summary>
[ByRefEvent]
public record struct GetLanguagesEvent(EntityUid Uid)
{
    public string Current = "";
    public Dictionary<string, LanguageKnowledge> Languages = new();
    public Dictionary<string, LanguageKnowledge> Translator = new();
}
