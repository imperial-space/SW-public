using Content.Shared.Examine;
using Content.Shared.Friends;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Identity;

public sealed partial class MedievalIdentitySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ComponentInit>(OnComponentInit, before: new[] { typeof(SharedFriendsSystem) });
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnComponentInit(EntityUid uid, IdentityRequiresKnowledgeComponent component, ComponentInit args)
    {
        component.Identifier = _nextId;
        _nextId++;
        Dirty(uid, component);
    }

    private void OnGetVerbs(EntityUid uid, IdentityRequiresKnowledgeComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<IdentityRequiresKnowledgeComponent>(args.User, out var userComp))
            return;
        if (uid == args.User)
            return;
        if (component.KnownIds.Contains(userComp.Identifier) || !userComp.HideUnknown)
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                _popup.PopupPredicted($"Вы представились {IdentityManagement.Identity.Name(uid, EntityManager, args.User)}.", null, uid, args.User);
                _popup.PopupPredicted($"{Name(args.User)} представился вам.", null, args.User, uid);
                component.KnownIds.Add(userComp.Identifier);
                Dirty(uid, component);
            },
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Medieval/date.rsi"), "date"),
            Priority = 1,
            Text = "Представиться"
        });
    }

    private void OnExamined(EntityUid uid, IdentityRequiresKnowledgeComponent component, ExaminedEvent args)
    {
        args.PushMarkup($"[font=Default size=8][color=gray]Идентификатор игрока:[/color] {component.Identifier}[/font]", -1);
    }
}
