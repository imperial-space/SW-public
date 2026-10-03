using Content.Server.Chat.Systems;
using Content.Shared.Imperial.Medieval.Language;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Language;

/// <summary>Resolves the physical origin without changing the speaker's language or authorship.</summary>
public sealed class ResolveLanguageSpeechSourceEvent(EntityUid speaker, LanguagePrototype language, bool whisper) : EntityEventArgs
{
    public readonly EntityUid Speaker = speaker;
    public readonly LanguagePrototype Language = language;
    public readonly bool Whisper = whisper;
    public EntityUid Source = speaker;
}

/// <summary>Allows alternate perception to supplement the ordinary recipients of this actual language.</summary>
public sealed class LanguageSpeechRecipientsEvent(
    EntityUid speaker,
    EntityUid source,
    LanguagePrototype language,
    bool whisper,
    Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> recipients) : EntityEventArgs
{
    public readonly EntityUid Speaker = speaker;
    public readonly EntityUid Source = source;
    public readonly LanguagePrototype Language = language;
    public readonly bool Whisper = whisper;
    public readonly Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> Recipients = recipients;
}

/// <summary>Checks one listener condition after its normal evaluation.</summary>
public sealed class LanguageListenerConditionEvent(
    EntityUid speaker,
    EntityUid source,
    LanguagePrototype language,
    bool whisper,
    ILanguageCondition condition,
    bool allowed) : EntityEventArgs
{
    public readonly EntityUid Speaker = speaker;
    public readonly EntityUid Source = source;
    public readonly LanguagePrototype Language = language;
    public readonly bool Whisper = whisper;
    public readonly ILanguageCondition Condition = condition;
    public bool Allowed = allowed;
}
