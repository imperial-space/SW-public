using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Containers;
using Content.Shared.Ghost;
using Robust.Shared.Network;
using Content.Shared.Hands.EntitySystems;
using System.Linq;
using Content.Shared.Implants.Components;

namespace Content.Shared.Imperial.Medieval.Language;

public abstract partial class SharedLanguageSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public static ProtoId<LanguagePrototype> Universal = "Universal";
    public static ProtoId<LanguagePrototype> Common = "Common";

    public override void Initialize()
    {
        InitializeImplants();
        InitializeTranslator();

        SubscribeLocalEvent<LanguageSpeakerComponent, GetLanguagesEvent>(OnGetLanguages);
    }

    private void OnGetLanguages(EntityUid uid, LanguageSpeakerComponent comp, ref GetLanguagesEvent args)
    {
        args.Current = comp.CurrentLanguage ?? Universal;
        args.Languages = comp.Languages;

        if (_container.TryGetContainer(uid, ImplanterComponent.ImplantSlotId, out var implantContainer))
        {
            foreach (var item in implantContainer.ContainedEntities)
            {
                RaiseLocalEvent(item, ref args);
            }
        }
    }

    public bool CanSpeak(EntityUid uid, LanguagePrototype proto)
    {
        return CanSpeak(uid, proto.ID);
    }

    public bool CanUnderstand(EntityUid uid, LanguagePrototype proto)
    {
        return CanUnderstand(uid, proto.ID);
    }

    public bool CanSpeak(EntityUid uid, string protoId)
    {
        if (!_proto.TryIndex<LanguagePrototype>(protoId, out var proto))
            return false;

        if (HasComp<GhostComponent>(uid))
            return false;

        if (HasComp<UniversalLanguageSpeakerComponent>(uid))
            return true;

        if (proto.ID == Universal)
            return true;

        if (!GetLanguagesKnowledged(uid, LanguageKnowledge.BadSpeak, out var langs, out var translator))
            return false;

        if (langs.ContainsKey(protoId))
            return true;

        return false;
    }

    public bool CanUnderstand(EntityUid uid, string protoId)
    {
        if (!_proto.TryIndex<LanguagePrototype>(protoId, out var proto))
            return false;

        if (HasComp<GhostComponent>(uid))
            return true;

        if (HasComp<UniversalLanguageSpeakerComponent>(uid))
            return true;

        if (proto.ID == Universal)
            return true;

        if (!GetLanguagesKnowledged(uid, LanguageKnowledge.Understand, out var langs, out var translator))
            return false;

        if (langs.ContainsKey(protoId))
            return true;

        return false;
    }

    public LanguagePrototype GetCurrentLanguage(EntityUid uid)
    {
        var universalProto = _proto.Index<LanguagePrototype>(Universal);

        if (!TryComp<LanguageSpeakerComponent>(uid, out var comp) || comp.CurrentLanguage == null)
            return universalProto;

        if (_proto.TryIndex<LanguagePrototype>(comp.CurrentLanguage, out var proto))
            return proto;

        return universalProto;
    }

    public void SelectDefaultLanguage(EntityUid uid, LanguageSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;
        if (!_netMan.IsServer)
            return;
        SortLanguages(uid);

        component.CurrentLanguage = component.Languages.Where(x => (int)x.Value >= 1).ToDictionary().Keys.FirstOrDefault("Universal");

        GetLanguages(uid, out var langs, out var translator, out var current);

        UpdateUi(uid);
    }

    public bool GetLanguages(
        EntityUid? player,
        out Dictionary<string, LanguageKnowledge> langs,
        out Dictionary<string, LanguageKnowledge> translator,
        out string current)
    {

        langs = new();
        translator = new();
        current = String.Empty;

        if (player == null)
            return false;
        var uid = player.Value;

        var ev = new GetLanguagesEvent(uid);
        RaiseLocalEvent(uid, ref ev);

        langs = ev.Languages;
        translator = ev.Translator;
        current = ev.Current;

        if ((translator.Count() <= 0 && langs.Count() <= 0) || current == String.Empty)
            return false;

        return true;
    }

    public bool GetLanguagesKnowledged(
        EntityUid? player,
        LanguageKnowledge required,
        out Dictionary<string, LanguageKnowledge> langs,
        out string current)
    {

        langs = new();
        current = String.Empty;

        if (player == null)
            return false;
        var uid = player.Value;

        var ev = new GetLanguagesEvent(uid);
        RaiseLocalEvent(uid, ref ev);

        langs = ev.Languages.Where(x => x.Value >= required).ToDictionary();
        foreach (var item in ev.Translator)
        {
            if (item.Value < required)
                continue;
            if (ev.Languages.ContainsKey(item.Key))
            {
                if (ev.Languages[item.Key] <= item.Value)
                    continue;
                langs[item.Key] = ev.Translator[item.Key];
            }
            else
                langs.Add(item.Key, item.Value);
        }

        current = ev.Current;

        if (langs.Count() <= 0 || current == String.Empty)
            return false;

        return true;
    }

    public LanguagePrototype GetLanguage(string id)
    {
        if (!_proto.TryIndex<LanguagePrototype>(id, out var result))
            return _proto.Index<LanguagePrototype>(Universal);

        return result;
    }

    public void AddSpokenLanguage(EntityUid uid, string lang, LanguageKnowledge knowledge = LanguageKnowledge.Speak, LanguageSpeakerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        if (!_proto.TryIndex<LanguagePrototype>(lang, out var proto))
            return;
        if (comp.Languages.ContainsKey(lang))
            comp.Languages[lang] = knowledge;
        else
            comp.Languages.Add(lang, knowledge);

        UpdateUi(uid, comp);
    }

    public void SortLanguages(EntityUid uid, LanguageSpeakerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        var list = comp.Languages.ToList();
        list.Sort((x, y) => _proto.Index<LanguagePrototype>(x.Key).LocalizedName[0].CompareTo(_proto.Index<LanguagePrototype>(y.Key).LocalizedName[0]));
        list.Sort((x, y) => _proto.Index<LanguagePrototype>(y.Key).Priority.CompareTo(_proto.Index<LanguagePrototype>(x.Key).Priority));
        list.Sort((x, y) => CanSpeak(uid, y.Key).CompareTo(CanSpeak(uid, x.Key)));

        comp.Languages = list.ToDictionary();
    }

    public virtual void UpdateUi(EntityUid uid, LanguageSpeakerComponent? comp = null)
    {
        SortLanguages(uid);
    }
}
