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

        SubscribeLocalEvent<IndentityRequiresKnowledgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<IndentityRequiresKnowledgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnComponentInit(EntityUid uid, IndentityRequiresKnowledgeComponent component, ComponentInit args)
    {
        component.Identifier = _nextId;
        _nextId++;
        Dirty(uid, component);
    }

    private void OnGetVerbs(EntityUid uid, IndentityRequiresKnowledgeComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<IndentityRequiresKnowledgeComponent>(uid, out var comp))
            return;
        if (comp.KnownIds.Contains(component.Identifier))
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                _popup.PopupPredicted($"Вы представились {IdentityManagement.Identity.Name(uid, EntityManager, args.User)}.", uid, args.User);
                _popup.PopupPredicted($"{Name(args.User)} представился вам.", args.User, uid);
                component.KnownIds.Add(comp.Identifier);
                Dirty(uid, component);
            },
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Medieval/date.rsi"), "date"),
            Priority = 1,
            Text = "Представиться"
        });
    }
}
