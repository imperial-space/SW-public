using Content.Server.Imperial.Medieval.Chat;
using Content.Server.Radio;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Medieval.Language;

public sealed partial class LanguageSystem
{
    private void InitializeTraits()
    {
        SubscribeLocalEvent<DeafTraitComponent, MapInitEvent>(OnDeafInit);
        SubscribeLocalEvent<DeafComponent, CanHearVoiceEvent>(OnCanHear);
        SubscribeLocalEvent<DeafComponent, RadioReceiveAttemptEvent>(OnRadioRecieveAttempt);
    }

    private void OnDeafInit(EntityUid uid, DeafTraitComponent comp, MapInitEvent args)
    {
        if (!TryComp<LanguageSpeakerComponent>(uid, out var language))
            return;
        EnsureComp<DeafComponent>(uid);
        language.Languages.Clear();
        AddSpokenLanguage(uid, "SignLanguage");
        SelectDefaultLanguage(uid);
        UpdateUi(uid);
    }

    private void OnCanHear(EntityUid uid, DeafComponent comp, ref CanHearVoiceEvent args)
    {
        args.Cancelled = true;
        if (args.Whisper)
            return;

        _popup.PopupEntity(Loc.GetString("popup-deaf-cannot-hear", ("entity", Identity.Entity(args.Source, EntityManager))),
                            args.Source,
                            uid,
                            PopupType.Small);
    }

    private void OnRadioRecieveAttempt(EntityUid uid, DeafComponent comp, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled = true;
    }

}
