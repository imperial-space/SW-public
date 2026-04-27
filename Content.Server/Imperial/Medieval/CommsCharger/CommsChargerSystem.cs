using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.ChargeableAnnounce;
using Content.Shared.Imperial.Medieval.CommsCharger;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.Medieval.CommsCharger;

public sealed class CommsChargerSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChargeableAnnounceComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ChargeableAnnounceComponent, CommsChargerDoAfterEvent>(OnCommsChargerDoAfter);
        SubscribeLocalEvent<CommsChargerComponent, ComponentShutdown>(OnCommsChargerShutdown);
    }

    private void OnAfterInteract(EntityUid uid, ChargeableAnnounceComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || comp.IsCharged || !args.CanReach)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        if (!TryComp<CommsChargerComponent>(target, out var commsCharger))
            return;

        if (commsCharger.Faction != receiver.Faction)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.RechargeDelay,
            new CommsChargerDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        args.Handled = true;
    }

    private void OnCommsChargerDoAfter(EntityUid uid, ChargeableAnnounceComponent comp, ref CommsChargerDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target || comp.IsCharged)
            return;

        if (!TryComp<CloackRecieverComponent>(uid, out var receiver))
            return;

        if (!TryComp<CommsChargerComponent>(target, out var commsCharger))
            return;

        if (commsCharger.Faction != receiver.Faction)
            return;

        comp.IsCharged = true;
        Dirty(uid, comp);
    }

    private void OnCommsChargerShutdown(EntityUid uid, CommsChargerComponent comp, ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<MedievalFactionMemberComponent, CloackMessageComponent>();
        while (query.MoveNext(out var memberUid, out var factionMember, out var cloackMessage))
        {
            if (cloackMessage.Faction != comp.Faction)
                continue;

            if (factionMember.MenuAccess != FactionMenuAccess.Full)
                continue;

            RemComp<CloackMessageComponent>(memberUid);
            return;
        }
    }
}
