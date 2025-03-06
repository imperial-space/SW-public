using Robust.Shared.Serialization;

namespace Content.Shared.ADT.Language;

/// <summary>
/// Отправляется клиентом при выборе языка в меню
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguageChosenMessage : EntityEventArgs
{
    public NetEntity Uid;
    public string SelectedLanguage;

    public LanguageChosenMessage(NetEntity uid, string selectedLanguage)
    {
        Uid = uid;
        SelectedLanguage = selectedLanguage;
    }
}
