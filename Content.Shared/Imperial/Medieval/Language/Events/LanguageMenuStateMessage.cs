using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Language;

/// <summary>
/// Отправляется сервером на клиент для обновления меню языков
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguageMenuStateMessage : EntityEventArgs
{
    public NetEntity ComponentOwner;
    public string CurrentLanguage;
    public Dictionary<string, LanguageKnowledge> Options;
    public Dictionary<string, LanguageKnowledge> TranslatorOptions;

    public LanguageMenuStateMessage(NetEntity componentOwner, string currentLanguage, Dictionary<string, LanguageKnowledge> options, Dictionary<string, LanguageKnowledge> translatorOptions)
    {
        ComponentOwner = componentOwner;
        CurrentLanguage = currentLanguage;
        Options = options;
        TranslatorOptions = translatorOptions;
    }
}
