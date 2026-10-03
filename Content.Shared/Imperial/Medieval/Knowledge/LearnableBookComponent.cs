using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Knowledge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LearnableBookComponent : Component
{
    [DataField, AutoNetworkedField] public string Knowledge = string.Empty;
    [DataField, AutoNetworkedField] public string Language = "Common";
    [DataField, AutoNetworkedField] public bool Encrypted;
    [DataField, AutoNetworkedField] public bool Original = true;
    [DataField, AutoNetworkedField] public bool Spent;
    [DataField] public string? Translator;
    /// <summary>Optional overrides; zero uses the knowledge tier's reading time.</summary>
    [DataField, AutoNetworkedField] public float StudySeconds;
    [DataField, AutoNetworkedField] public float TranslationSeconds;
    // Server-only plaintext, including the original story of an encrypted edition.
    [DataField] public string? Text;
    [DataField] public string? Title;
    public DoAfterId? Reading;
    public EntityUid? TranslationTarget;
}

[RegisterComponent]
public sealed partial class BookManuscriptComponent : Component
{
    public EntityUid Source;
    public string Language = string.Empty;
    public EntityUid Writer;
    public DoAfterId? Writing;
}

[Serializable, NetSerializable]
public sealed partial class StudyBookDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TranslateBookDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class StudyBookMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FocusKnowledgeBookMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TranslateBookMessage(NetEntity source, string language) : BoundUserInterfaceMessage
{
    public NetEntity Source = source;
    public string Language = language;
}
