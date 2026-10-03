using System.Linq;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Speech;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Paper;
using Content.Shared.Speech;
using Content.Shared.Verbs;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.BookAbilities;

[RegisterComponent]
public sealed partial class BookScrollActionComponent : Component
{
    [DataField] public EntityUid Scroll;
    public EntityUid? ReservedBy;
}

[RegisterComponent]
public sealed partial class BookMagicTraceComponent : Component
{
    [DataField] public string Spell = string.Empty;
    [DataField] public string Race = string.Empty;
    [DataField] public string Signature = string.Empty;
    [DataField] public int Tier;
    [DataField] public TimeSpan CastAt;
}

public sealed partial class MedievalBookAbilitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ManaSystem _mana = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly ChatSystem _voiceChat = default!;
    [Dependency] private readonly IConfigurationManager _voiceConfig = default!;
    [Dependency] private readonly LanguageSystem _voiceLanguage = default!;
    [Dependency] private readonly SpeechSoundSystem _voiceSounds = default!;
    [Dependency] private readonly IRobustRandom _voiceRandom = default!;

    private static readonly ProtoId<SpeechSoundsPrototype>[] ObjectVoices = ["Bass", "Baritone", "Tenor", "Alto"];

    private sealed record VoiceRequest(int Id, EntityUid Source, ICommonSession Session, TimeSpan Until)
    {
        // Set only during one synchronous call to the regular speech pipeline.
        public bool Speaking;
    }

    private readonly Dictionary<EntityUid, VoiceRequest> _voices = new();
    private int _nextVoiceRequestId;
    private readonly Dictionary<EntityUid, string> _signatures = new();

    private void InitializeSpeechAndMagic()
    {
        SubscribeLocalEvent<MetaDataComponent, MedievalAfterCastSpellEvent>(OnSpellCast);
        SubscribeLocalEvent<BookMagicTraceComponent, ExaminedEvent>(OnTraceExamined);
        SubscribeLocalEvent<BookSpellScrollComponent, GetItemActionsEvent>(OnScrollActions);
        SubscribeLocalEvent<BookScrollActionComponent, MedievalBeforeCastSpellEvent>(OnScrollBeforeCast);
        SubscribeLocalEvent<BookScrollActionComponent, MedievalFailCastSpellEvent>(OnScrollFailed);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookReadTracesActionEvent>(OnReadTracesAction);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookVentriloquismActionEvent>(OnProjectVoiceAction);
        SubscribeNetworkEvent<BookVoiceSubmittedEvent>(OnVoiceSubmitted);
        SubscribeLocalEvent<LearnedKnowledgeComponent, MindRemovedMessage>(OnVoiceMindRemoved);
        SubscribeLocalEvent<LearnedKnowledgeComponent, ResolveLanguageSpeechSourceEvent>(OnVoiceSource);
        SubscribeLocalEvent<LearnedKnowledgeComponent, EntitySpokeEvent>(OnVoiceSpoken, before: [typeof(SpeechSoundSystem)]);
        SubscribeLocalEvent<LanguageSpeechRecipientsEvent>(OnVisualSpeechRecipients);
        SubscribeLocalEvent<LearnedKnowledgeComponent, LanguageListenerConditionEvent>(OnVisualSpeechCondition);
    }

    private void OnVoiceSource(EntityUid uid, LearnedKnowledgeComponent comp, ResolveLanguageSpeechSourceEvent args)
    {
        if (VoiceSource(uid) is var source && source != uid)
            args.Source = source;
    }

    private void OnVoiceSpoken(EntityUid uid, LearnedKnowledgeComponent comp, EntitySpokeEvent args)
    {
        var source = VoiceSource(uid);
        if (source == uid)
            return;

        args.SoundSource = source;
        args.OverrideSpeechSound = true;
        args.SpeechSound = null;
        if (HasComp<HumanoidAppearanceComponent>(source))
        {
            if (TryComp<SpeechComponent>(source, out var speech))
                args.SpeechSound = _voiceSounds.GetSpeechSound((source, speech), args.Message);
            return;
        }

        // Objects have no personal voice. Pick a human sound set for this one utterance.
        var voices = ObjectVoices.Where(voice => _prototypes.HasIndex(voice)).ToArray();
        if (voices.Length > 0)
            args.SpeechSound = _voiceSounds.GetSpeechSound(_voiceRandom.Pick(voices),
                AudioParams.Default.WithVolume(-2f).WithRolloffFactor(4.5f), args.Message);
    }

    private void OnVisualSpeechRecipients(LanguageSpeechRecipientsEvent args)
    {
        // Normal recipients, including remote listeners supplied by other systems, stay intact.
        if (!args.Whisper || args.Source != args.Speaker)
            return;

        var query = EntityQueryEnumerator<ActorComponent, LearnedKnowledgeComponent>();
        while (query.MoveNext(out var listener, out var actor, out _))
        {
            if (!CanLipRead(listener, args.Speaker, args.Language.ID))
                continue;

            var session = actor.PlayerSession;
            if (args.Recipients.TryGetValue(session, out var existing))
            {
                args.Recipients[session] = existing with { Muffled = false };
                continue;
            }

            var distance = (_transform.GetMapCoordinates(listener).Position -
                            _transform.GetMapCoordinates(args.Source).Position).Length();
            args.Recipients.Add(session, new ChatSystem.ICChatRecipientData(distance, false));
        }
    }

    private void OnVisualSpeechCondition(EntityUid uid, LearnedKnowledgeComponent comp, LanguageListenerConditionEvent args)
    {
        if (!args.Allowed && args.Condition is CanHear && args.Source == args.Speaker &&
            CanLipRead(uid, args.Speaker, args.Language.ID))
            args.Allowed = true;
    }

    private void OnReadTracesAction(EntityUid uid, LearnedKnowledgeComponent comp, BookReadTracesActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookMagicTraces")) return;
        ReadTraces(uid);
        args.Handled = true;
    }

    private void OnProjectVoiceAction(EntityUid uid, LearnedKnowledgeComponent comp, BookVentriloquismActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor)) return;
        if (!CanProjectVoice(uid, args.Target))
        {
            AbilityError(uid, "book-voice-unavailable");
            return;
        }

        var id = ++_nextVoiceRequestId;
        _voices[uid] = new(id, args.Target, actor.PlayerSession, _timing.CurTime + TimeSpan.FromSeconds(60));
        RaiseNetworkEvent(new BookVoicePromptEvent(id, Identity.Name(args.Target, EntityManager, uid),
            _voiceConfig.GetCVar(CCVars.ChatMaxMessageLength)), actor.PlayerSession);
        args.Handled = true;
    }

    private void OnVoiceMindRemoved(EntityUid uid, LearnedKnowledgeComponent comp, MindRemovedMessage args)
    {
        _voices.Remove(uid);
    }

    private bool CanProjectVoice(EntityUid user, EntityUid target)
    {
        if (!Exists(user) || !Exists(target) || !Knows(user, "BookVentriloquism") ||
            !_abilityBlocker.CanInteract(user, null) || !_abilityBlocker.CanSpeak(user) ||
            !_examine.InRangeUnOccluded(user, target, 8f)) return false;

        // A physical voice uses the performer's spoken language, never a target's languages or telepathy.
        var language = _voiceLanguage.GetCurrentLanguage(user);
        return language.Vocal && language.LanguageType is Generic && _voiceLanguage.CanSpeak(user, language) &&
            language.Conditions.Where(condition => !condition.RaiseOnListener)
                .All(condition => condition.Condition(user, null, EntityManager));
    }

    private void OnVoiceSubmitted(BookVoiceSubmittedEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user ||
            !TryComp<ActorComponent>(user, out var actor) || actor.PlayerSession != session.SenderSession ||
            !_voices.TryGetValue(user, out var request) || request.Id != args.RequestId ||
            request.Session != session.SenderSession || request.Speaking) return;

        // Consume before sending; cancelled, expired and replayed requests cannot be reused.
        _voices.Remove(user);
        if (string.IsNullOrWhiteSpace(args.Text)) return;
        if (request.Until < _timing.CurTime || args.Text.Length > _voiceConfig.GetCVar(CCVars.ChatMaxMessageLength) ||
            !CanProjectVoice(user, request.Source))
        {
            AbilityError(user, "book-voice-expired");
            return;
        }

        request.Speaking = true;
        _voices[user] = request;
        try
        {
            _voiceChat.TrySendInGameICMessage(user, args.Text.Trim(), InGameICChatType.Speak,
                ChatTransmitRange.Normal, player: session.SenderSession, checkRadioPrefix: false);
        }
        finally
        {
            _voices.Remove(user);
        }
    }

    public bool CanLipRead(EntityUid listener, EntityUid source, string language)
    {
        return Knows(listener, "BookLipReading") && HasComp<HumanoidAppearanceComponent>(source) &&
            TryComp<LanguageSpeakerComponent>(listener, out var languages) && languages.Languages.ContainsKey(language) &&
            _examine.InRangeUnOccluded(source, listener, 8f);
    }

    public EntityUid VoiceSource(EntityUid speaker)
    {
        return _voices.TryGetValue(speaker, out var voice) && voice.Speaking ? voice.Source : speaker;
    }

    private bool HasPen(EntityUid user) => _hands.EnumerateHeld(user).Any(item => _tags.HasTag(item, "Write"));

    private bool HasScribingMana(EntityUid user, EntityUid spell)
    {
        var cost = TryComp<ManaDrainSpellComponent>(spell, out var drain) ? drain.ManaDrain : 0;
        return TryComp<ManaComponent>(user, out var mana) && mana.Mana - mana.CastedSpells.Values.Sum() >= cost;
    }

    private void Scribe(EntityUid user, EntityUid sheet, EntityUid originalAction, string prototype)
    {
        if (!HasScribingMana(user, originalAction)) return;
        var cost = TryComp<ManaDrainSpellComponent>(originalAction, out var drain) ? drain.ManaDrain : 0;
        var scroll = EnsureComp<BookSpellScrollComponent>(sheet);
        scroll.Spell = prototype;
        if (!_actions.AddAction(user, ref scroll.Action, prototype, sheet) || scroll.Action is not { } action)
        {
            RemComp<BookSpellScrollComponent>(sheet);
            return;
        }
        if (TryComp<ManaComponent>(user, out var mana)) _mana.TryChangeMana(user, mana.Mana - cost, mana);
        RemComp<ManaDrainSpellComponent>(action);
        EnsureComp<BookScrollActionComponent>(action).Scroll = sheet;
        _paper.SetContent(sheet, Loc.GetString("book-ability-scroll-content", ("spell", Name(originalAction))));
        _meta.SetEntityName(sheet, Loc.GetString("book-ability-scroll-name", ("spell", Name(originalAction))));
        Comp<PaperComponent>(sheet).EditingDisabled = true;
        Dirty(sheet, Comp<PaperComponent>(sheet));
    }

    private void OnScrollActions(EntityUid uid, BookSpellScrollComponent comp, GetItemActionsEvent args)
    {
        if (comp.Spent || comp.Action == null) return;
        args.AddAction(ref comp.Action, comp.Spell);
    }

    private void OnScrollBeforeCast(EntityUid uid, BookScrollActionComponent comp, ref MedievalBeforeCastSpellEvent args)
    {
        if (args.Cancelled)
        {
            args.HasResourceReservation |= args.IsContinuation && comp.ReservedBy == args.Performer;
            return;
        }
        if (!TryComp<BookSpellScrollComponent>(comp.Scroll, out var scroll) || scroll.Spent ||
            !_hands.IsHolding(args.Performer, comp.Scroll, out _) ||
            (args.IsContinuation ? comp.ReservedBy != args.Performer : comp.ReservedBy != null))
        {
            args.Cancelled = true;
            return;
        }
        comp.ReservedBy = args.Performer;
        args.HasResourceReservation = true;
    }

    private void OnScrollFailed(EntityUid uid, BookScrollActionComponent comp, MedievalFailCastSpellEvent args)
    {
        if (comp.ReservedBy == args.Performer) comp.ReservedBy = null;
    }

    private void OnSpellCast(EntityUid uid, MetaDataComponent comp, MedievalAfterCastSpellEvent args)
    {
        if (!Exists(args.Performer)) return;
        if (!_signatures.TryGetValue(args.Performer, out var signature))
        {
            signature = Guid.NewGuid().ToString("N")[..8];
            _signatures[args.Performer] = signature;
        }
        var trace = Spawn("MedievalBookMagicTrace", Transform(args.Performer).Coordinates);
        var info = EnsureComp<BookMagicTraceComponent>(trace);
        info.Spell = Name(args.Action);
        var spellId = MetaData(args.Action).EntityPrototype?.ID ?? string.Empty;
        info.Tier = spellId.Contains("Senior") ? 3 : spellId.Contains("Middle") ? 2 : 1;
        info.Signature = signature;
        info.Race = TryComp<HumanoidAppearanceComponent>(args.Performer, out var appearance) ? appearance.Species : "Unknown";
        info.CastAt = _timing.CurTime;
        if (TryComp<BookScrollActionComponent>(uid, out var linked) &&
            TryComp<BookSpellScrollComponent>(linked.Scroll, out var scroll) && !scroll.Spent)
        {
            scroll.Spent = true;
            _actions.RemoveAction(args.Performer, uid);
            QueueDel(linked.Scroll);
        }
    }

    private string TraceText(BookMagicTraceComponent comp) => Loc.GetString("book-ability-trace", ("spell", comp.Spell),
        ("race", comp.Race), ("tier", comp.Tier), ("signature", comp.Signature), ("seconds", (int) (_timing.CurTime - comp.CastAt).TotalSeconds));

    private void OnTraceExamined(EntityUid uid, BookMagicTraceComponent comp, ExaminedEvent args)
    {
        if (Knows(args.Examiner, "BookMagicTraces")) args.PushText(TraceText(comp));
    }

    private void ReadTraces(EntityUid user)
    {
        var lines = _lookup.GetEntitiesInRange(Transform(user).Coordinates, 6f)
            .Where(e => HasComp<BookMagicTraceComponent>(e) && _examine.InRangeUnOccluded(user, e, 6f))
            .Select(e => TraceText(Comp<BookMagicTraceComponent>(e))).ToArray();
        _popup.PopupEntity(lines.Length == 0 ? Loc.GetString("book-ability-no-traces") : string.Join("\n", lines), user, user);
    }

}
