using Content.Server.Chat.Managers;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Nocturn.Components;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server.Nocturn;

public sealed class MedievalInquisitorMedallionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RaceSystem _race = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalInquisitorMedallionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MedievalInquisitorMedallionComponent, MedievalInquisitorMedallionDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<NocturnHumanBloodProhibitionComponent, NocturnDrinkActionEvent>(
            OnRestrictedDrinkAction,
            before: new[] { typeof(RaceSystem) });
    }

    private void OnAfterInteract(Entity<MedievalInquisitorMedallionComponent> medallion, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!TryComp(medallion, out UseDelayComponent? useDelay) || _delay.IsDelayed((medallion, useDelay)))
            return;

        var targetKind = GetTargetKind(target, medallion.Comp);
        if (targetKind == null)
        {
            _popup.PopupEntity(
                Loc.GetString("medieval-inquisitor-medallion-invalid-target"),
                target,
                args.User,
                PopupType.MediumCaution);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            medallion.Comp.ExaminationDuration,
            new MedievalInquisitorMedallionDoAfterEvent(targetKind.Value),
            medallion,
            target,
            medallion)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
            BlockDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        ShowStartMessages(args.User, target, targetKind.Value);
    }

    private void OnDoAfter(
        Entity<MedievalInquisitorMedallionComponent> medallion,
        ref MedievalInquisitorMedallionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        _delay.TryResetDelay(medallion);

        if (args.Target is not { } target)
            return;

        switch (args.TargetKind)
        {
            case InquisitorMedallionTargetKind.Human:
                HandleHumanResult(args.User, target);
                break;
            case InquisitorMedallionTargetKind.YoungNocturne:
                HandleYoungNocturneResult(args.User, target);
                break;
            case InquisitorMedallionTargetKind.AncientNocturne:
                HandleAncientNocturneResult(args.User, target, medallion.Comp);
                break;
        }
    }

    private void OnRestrictedDrinkAction(
        Entity<NocturnHumanBloodProhibitionComponent> ent,
        ref NocturnDrinkActionEvent args)
    {
        if (args.Handled || !HasComp<HumanoidAppearanceComponent>(args.Target))
            return;

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("medieval-inquisitor-medallion-blood-drinking-blocked"),
            ent,
            ent,
            PopupType.Medium);
    }

    private void ShowStartMessages(
        EntityUid user,
        EntityUid target,
        InquisitorMedallionTargetKind targetKind)
    {
        _popup.PopupEntity(
            Loc.GetString("medieval-inquisitor-medallion-examination-start-user"),
            user,
            user,
            PopupType.Medium);

        var message = targetKind switch
        {
            InquisitorMedallionTargetKind.Human => "medieval-inquisitor-medallion-examination-start-human",
            InquisitorMedallionTargetKind.YoungNocturne => "medieval-inquisitor-medallion-examination-start-young-nocturne",
            InquisitorMedallionTargetKind.AncientNocturne => "medieval-inquisitor-medallion-examination-start-ancient-nocturne",
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, null)
        };
        var popupType = targetKind switch
        {
            InquisitorMedallionTargetKind.Human => PopupType.Medium,
            InquisitorMedallionTargetKind.YoungNocturne => PopupType.MediumCaution,
            InquisitorMedallionTargetKind.AncientNocturne => PopupType.LargeCaution,
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, null)
        };

        _popup.PopupEntity(Loc.GetString(message), target, target, popupType);
    }

    private void HandleHumanResult(EntityUid user, EntityUid target)
    {
        _popup.PopupEntity(
            Loc.GetString("medieval-inquisitor-medallion-result-human-target"),
            target,
            target,
            PopupType.Medium);
        _popup.PopupEntity(
            Loc.GetString("medieval-inquisitor-medallion-result-human-user"),
            target,
            user,
            PopupType.Medium);
    }

    private void HandleYoungNocturneResult(EntityUid user, EntityUid target)
    {
        if (TryComp<NocturnComponent>(target, out var nocturn) &&
            nocturn.IsDisguised &&
            TryComp<HumanoidAppearanceComponent>(target, out var appearance))
        {
            _race.RevertToOriginalForm(target, nocturn, appearance);
        }

        EnsureComp<NocturnHumanBloodProhibitionComponent>(target);

        var targetMessage = Loc.GetString("medieval-inquisitor-medallion-result-young-nocturne-target");
        _popup.PopupEntity(targetMessage, target, target, PopupType.Medium);

        if (TryComp<ActorComponent>(target, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, targetMessage);

        _popup.PopupEntity(
            Loc.GetString(
                "medieval-inquisitor-medallion-result-young-nocturne-user",
                ("target", target)),
            target,
            user,
            PopupType.Medium);
    }

    private void HandleAncientNocturneResult(
        EntityUid user,
        EntityUid target,
        MedievalInquisitorMedallionComponent medallion)
    {
        _popup.PopupEntity(
            Loc.GetString(
                "medieval-inquisitor-medallion-result-ancient-nocturne-user",
                ("target", target)),
            target,
            user,
            PopupType.LargeCaution);
        _damageable.TryChangeDamage(
            target,
            medallion.AncientNocturneDamage,
            ignoreResistances: true,
            origin: user);
    }

    private InquisitorMedallionTargetKind? GetTargetKind(
        EntityUid target,
        MedievalInquisitorMedallionComponent medallion)
    {
        if (HasComp<AncientNocturneComponent>(target))
            return InquisitorMedallionTargetKind.AncientNocturne;

        if (HasComp<NocturnComponent>(target))
            return InquisitorMedallionTargetKind.YoungNocturne;

        if (TryComp<HumanoidAppearanceComponent>(target, out var appearance) &&
            appearance.Species == medallion.HumanSpecies)
        {
            return InquisitorMedallionTargetKind.Human;
        }

        return null;
    }
}
