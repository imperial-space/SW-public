using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.IdentityManagement;

public abstract class SharedMedievalIdentitySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ExaminedEvent>(OnExamined);
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
                _popup.PopupPredicted($"Вы представились {Identity.Name(uid, EntityManager, args.User)}.", null, uid, args.User);
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
    public bool IsIdentityMasked(EntityUid entity)
    {
        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(entity, ev);
        return ev.Cancelled;  // Если отменено, то идентичность заблокирована (маска или полный coverage)
    }
}
