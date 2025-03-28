using Content.Server.Chat.Systems;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;
using Content.Server.Chat.Managers;
using Robust.Shared.Replays;
using Content.Shared.IdentityManagement;
using Content.Server.Examine;

namespace Content.Server.Imperial.Medieval.Language;
/// <summary>
/// Стандартный вид языка. Знающие видят текст, незнающие - билиберду
/// </summary>
[DataDefinition]
public sealed partial class Generic : ILanguageType
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public Color? Color { get; set; }

    [DataField]
    public Color? WhisperColor { get; set; }

    /// <inheritdoc/>
    [DataField]
    public bool RaiseEvent { get; set; } = true;

    /// <summary>
    /// Слоги/фразы, из которых составляется "неизвестное" сообщение
    /// </summary>
    [DataField(required: true)]
    public List<string> Replacement = new();

    /// <summary>
    /// От значения данного поля зависит то, будет язык заменять слоги, или фразы целиком
    /// true - слоги, false - фразы
    /// </summary>
    [DataField("obfuscateSyllables")]
    public bool ObfuscateSyllables { get; private set; } = false;

    /// <inheritdoc/>
    [DataField("verbs")]
    public Dictionary<string, List<string>> SuffixSpeechVerbs { get; set; } = new()
    {
        { "chat-speech-verb-suffix-exclamation-strong", new() },
        { "chat-speech-verb-suffix-exclamation", new() },
        { "chat-speech-verb-suffix-question", new() },
        { "chat-speech-verb-suffix-stutter", new() },
        { "chat-speech-verb-suffix-mumble", new() },
        { "Default", new() },
    };

    /// <inheritdoc/>
    [DataField]
    public int? FontSize { get; set; } = null;

    /// <inheritdoc/>
    [DataField]
    public string? Font { get; set; } = null;

    public void Speak(EntityUid uid, string message, string name, SpeechVerbPrototype verb, ChatTransmitRange range, IEntityManager entMan, out bool success, out string resultMessage, Color? colorOverride = null)
    {
        var lang = entMan.System<LanguageSystem>();
        var chat = entMan.System<ChatSystem>();
        var chatMan = IoCManager.Resolve<IChatManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var proto = IoCManager.Resolve<IPrototypeManager>();
        success = false;

        message = chat.TransformSpeech(uid, message);

        string coloredMessage = lang.AccentuateMessage(uid, Language, message);
        string coloredLanguageMessage = lang.ObfuscateMessage(uid, message, Replacement, ObfuscateSyllables);
        resultMessage = FormattedMessage.EscapeText(coloredMessage);
        if (string.IsNullOrEmpty(coloredMessage))
            return;

        // Apply language color
        if (colorOverride != null)
        {
            coloredMessage = $"[color={colorOverride.Value.ToHex()}]" + coloredMessage + "[/color]";
            coloredLanguageMessage = $"[color={colorOverride.Value.ToHex()}]" + coloredLanguageMessage + "[/color]";
        }
        else if (Color != null)
        {
            coloredMessage = $"[color={Color.Value.ToHex()}]" + coloredMessage + "[/color]";
            coloredLanguageMessage = $"[color={Color.Value.ToHex()}]" + coloredLanguageMessage + "[/color]";
        }

        // Getting verbs
        List<string> verbStrings = verb.SpeechVerbStrings;
        bool verbsReplaced = false;
        foreach (var str in ILanguageType.SpeechSuffixes)
        {
            if (message.EndsWith(Loc.GetString(str)) && SuffixSpeechVerbs.TryGetValue(str, out var strings) && strings.Count > 0)
            {
                verbStrings = strings;
                verbsReplaced = true;
            }
        }

        if (!verbsReplaced && SuffixSpeechVerbs.TryGetValue("Default", out var defaultStrings) && defaultStrings.Count > 0)
            verbStrings = defaultStrings;

        int fontSize = FontSize.HasValue ? FontSize.Value : verb.FontSize;
        string font = Font != null && Font != "" ? Font : verb.FontId;

        name = FormattedMessage.EscapeText(name);

        // Send
        success = true;

        var langProto = proto.Index(Language);
        foreach (var (session, data) in chat.GetRecipients(uid, ChatSystem.VoiceRange))
        {
            EntityUid listener;

            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;
            listener = session.AttachedEntity.Value;

            bool condition = true;
            foreach (var item in langProto.Conditions.Where(x => x.RaiseOnListener))
            {
                if (!item.Condition(listener, uid, entMan))
                    condition = false;
            }
            if (!condition)
                continue;

            var entRange = chat.MessageRangeCheck(session, data, range);
            if (entRange == ChatSystem.MessageRangeCheckResult.Disallowed)
                continue;
            var entHideChat = entRange == ChatSystem.MessageRangeCheckResult.HideChat;

            if (!lang.CanUnderstand(listener, langProto))
            {
                var wrappedLanguageMessage = Loc.GetString(verb.Bold && Font == null ? "chat-manager-entity-lang-say-bold-wrap-message" : "chat-manager-entity-lang-say-wrap-message",
                    ("entityName", Identity.Name(uid, entMan, listener, true)),
                    ("verb", Loc.GetString(random.Pick(verbStrings))),
                    ("fontType", font),
                    ("fontSize", fontSize),
                    ("defaultFont", verb.FontId),
                    ("defaultSize", verb.FontSize),
                    ("message", coloredLanguageMessage));

                chatMan.ChatMessageToOne(ChatChannel.Local, message, wrappedLanguageMessage, uid, entHideChat, session.Channel);
            }
            else
            {
                var wrappedMessage = Loc.GetString(verb.Bold && Font == null ? "chat-manager-entity-lang-say-bold-wrap-message" : "chat-manager-entity-lang-say-wrap-message",
                    ("entityName", Identity.Name(uid, entMan, listener)),
                    ("verb", Loc.GetString(random.Pick(verbStrings))),
                    ("fontType", font),
                    ("fontSize", fontSize),
                    ("defaultFont", verb.FontId),
                    ("defaultSize", verb.FontSize),
                    ("message", coloredMessage));

                chatMan.ChatMessageToOne(ChatChannel.Local, message, wrappedMessage, uid, entHideChat, session.Channel);
            }
        }
    }

    public void Whisper(EntityUid uid, string message, string name, string nameIdentity, ChatTransmitRange range, IEntityManager entMan, out bool success, out string resultMessage, out string resultObfMessage, Color? colorOverride = null)
    {
        var lang = entMan.System<LanguageSystem>();
        var chat = entMan.System<ChatSystem>();
        var examine = entMan.System<ExamineSystem>();
        var chatMan = IoCManager.Resolve<IChatManager>();
        var proto = IoCManager.Resolve<IPrototypeManager>();
        success = false;

        message = chat.TransformSpeech(uid, message);

        var accentMessage = lang.AccentuateMessage(uid, Language, message);
        var languageMessage = lang.ObfuscateMessage(uid, message, Replacement, ObfuscateSyllables);
        var obfuscatedMessage = chat.ObfuscateMessageReadability(accentMessage, 0.2f);
        var obfuscatedLanguageMessage = chat.ObfuscateMessageReadability(languageMessage, 0.2f);
        resultMessage = FormattedMessage.EscapeText(accentMessage);
        resultObfMessage = FormattedMessage.EscapeText(obfuscatedMessage);
        if (string.IsNullOrEmpty(accentMessage))
            return;

        if (colorOverride != null)
        {
            accentMessage = $"[color={colorOverride.Value.ToHex()}]" + accentMessage + "[/color]";
            languageMessage = $"[color={colorOverride.Value.ToHex()}]" + languageMessage + "[/color]";
            obfuscatedMessage = $"[color={colorOverride.Value.ToHex()}]" + obfuscatedMessage + "[/color]";
            obfuscatedLanguageMessage = $"[color={colorOverride.Value.ToHex()}]" + obfuscatedLanguageMessage + "[/color]";
        }
        else if (WhisperColor != null)
        {
            accentMessage = $"[color={WhisperColor.Value.ToHex()}]" + accentMessage + "[/color]";
            languageMessage = $"[color={WhisperColor.Value.ToHex()}]" + languageMessage + "[/color]";
            obfuscatedMessage = $"[color={WhisperColor.Value.ToHex()}]" + obfuscatedMessage + "[/color]";
            obfuscatedLanguageMessage = $"[color={WhisperColor.Value.ToHex()}]" + obfuscatedLanguageMessage + "[/color]";
        }

        success = true;
        var langProto = proto.Index(Language);

        foreach (var (session, data) in chat.GetWhisperRecipients(uid, ChatSystem.WhisperClearRange, ChatSystem.WhisperMuffledRange))
        {
            EntityUid listener;

            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;
            listener = session.AttachedEntity.Value;

            bool condition = true;
            foreach (var item in langProto.Conditions.Where(x => x.RaiseOnListener))
            {
                if (!item.Condition(listener, uid, entMan))
                    condition = false;
            }
            if (!condition)
                continue;

            if (chat.MessageRangeCheck(session, data, range) != ChatSystem.MessageRangeCheckResult.Full)
                continue; // Won't get logged to chat, and ghosts are too far away to see the pop-up, so we just won't send it to them.

            if (!data.Muffled)
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-wrap-message",
                    ("entityName", Identity.Name(uid, entMan, listener, true)),
                    ("fontType", Font ?? "NotoSansDisplayItalic"),
                    ("fontSize", FontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", lang.CanUnderstand(listener, langProto) ? accentMessage : languageMessage));

                chatMan.ChatMessageToOne(ChatChannel.Whisper, message, wrappedMessage, uid, false, session.Channel);
            }

            //If listener is too far, they only hear fragments of the message
            else if (examine.InRangeUnOccluded(uid, listener, ChatSystem.WhisperMuffledRange))
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-wrap-message",
                    ("entityName", Identity.Name(uid, entMan, listener, true)),
                    ("fontType", Font ?? "NotoSansDisplayItalic"),
                    ("fontSize", FontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", lang.CanUnderstand(listener, langProto) ? obfuscatedMessage : obfuscatedLanguageMessage));

                chatMan.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedMessage, uid, false, session.Channel);
            }

            //If listener is too far and has no line of sight, they can't identify the whisperer's identity
            else
            {
                var wrappedMessage = Loc.GetString("chat-manager-entity-lang-whisper-unknown-wrap-message",
                    ("fontType", Font ?? "NotoSansDisplayItalic"),
                    ("fontSize", FontSize ?? 11),
                    ("defaultFont", "NotoSansDisplayItalic"),
                    ("defaultSize", 11),
                    ("message", lang.CanUnderstand(listener, langProto) ? obfuscatedMessage : obfuscatedLanguageMessage));

                chatMan.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedMessage, uid, false, session.Channel);
            }
        }
    }
}
