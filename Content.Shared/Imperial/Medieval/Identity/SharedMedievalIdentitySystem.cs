using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Verbs;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.IdentityManagement;

public abstract class SharedMedievalIdentitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ExaminedEvent>(OnExamined);
    }


    private void OnGetVerbs(EntityUid uid, IdentityRequiresKnowledgeComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            !CanIntroduce(args.User, uid, component))
            return;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                RaiseNetworkEvent(new MedievalIntroduceIdentityRequest(GetNetEntity(uid)));
            },
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Medieval/date.rsi"), "date"),
            Priority = 1,
            Text = Loc.GetString("imperial-hm-identity-intrd")
        });
    }

    public bool CanIntroduce(EntityUid introducer, EntityUid observer, IdentityRequiresKnowledgeComponent? observerComp = null)
    {
        if (introducer == observer ||
            !Resolve(observer, ref observerComp, false) ||
            !TryComp<IdentityRequiresKnowledgeComponent>(introducer, out var introducerComp))
        {
            return false;
        }

        return introducerComp.HideUnknown && !observerComp.KnownIds.Contains(introducerComp.Identifier);
    }

    private void OnExamined(EntityUid uid, IdentityRequiresKnowledgeComponent component, ExaminedEvent args)
    {
        if (args.IsInDetailsRange &&
            uid != args.Examiner &&
            TryComp<IdentityRequiresKnowledgeComponent>(args.Examiner, out var examiner) &&
            examiner.KnownIds.Contains(component.Identifier) &&
            IsIdentityMasked(uid))
        {
            args.PushMarkup(Loc.GetString("imperial-hm-identity-familiar"));
        }

        args.PushMarkup(Loc.GetString("imperial-hm-identity-id", ("name", component.Identifier)), -1);
    }
    public bool IsIdentityMasked(EntityUid entity)
    {
        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(entity, ev);
        return ev.Cancelled;  // Если отменено, то идентичность заблокирована (маска или полный coverage)
    }
}

[Serializable, NetSerializable]
public sealed class MedievalIntroduceIdentityRequest : EntityEventArgs
{
    public NetEntity Observer { get; set; }

    public MedievalIntroduceIdentityRequest(NetEntity observer)
    {
        Observer = observer;
    }
}
