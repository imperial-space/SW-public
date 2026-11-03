using Content.Shared.ActionBlocker;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.RPPanel;

public sealed class RPPanelInteractionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RPPanelCooldownSystem _cooldown = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<RPPanelInteractionEvent>(OnInteraction);
    }

    public bool CanInteract(EntityUid user, EntityUid target)
    {
        return user != target
            && !TerminatingOrDeleted(user)
            && !TerminatingOrDeleted(target)
            && HasComp<ActorComponent>(user)
            && HasComp<RPPanelMemberComponent>(target)
            && !HasComp<GhostComponent>(user)
            && !HasComp<GhostComponent>(target)
            && _actionBlocker.CanInteract(user, target);
    }

    public bool InRange(EntityUid user, EntityUid target, RPPanelMemberComponent component, RPPanelAction action)
    {
        var range = action switch
        {
            RPPanelAction.Look or RPPanelAction.Wave => component.DistantRange,
            RPPanelAction.Hug or RPPanelAction.Shake => component.ContactRange,
            _ => -1f
        };

        return range >= 0f
            && _transform.GetMapCoordinates(user).InRange(_transform.GetMapCoordinates(target), range)
            && _interaction.InRangeAndAccessible(user, target, range);
    }

    private void OnInteraction(RPPanelInteractionEvent args, EntitySessionEventArgs session)
    {
        if (_net.IsClient && !_timing.IsFirstTimePredicted)
            return;

        if (session.SenderSession.AttachedEntity is not { } user
            || !TryGetEntity(args.Target, out var target)
            || !TryComp<RPPanelMemberComponent>(target, out var component)
            || !CanInteract(user, target.Value)
            || !Enum.IsDefined(args.Action))
            return;

        if (!InRange(user, target.Value, component, args.Action))
        {
            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("rp-panel-too-far"), user, user);
            return;
        }

        if (!_cooldown.TryStartCooldown(target.Value, component, user))
        {
            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("rp-panel-cooldown"), user, user);
            return;
        }

        if (args.Action == RPPanelAction.Hug)
        {
            var previous = component.HugInitiator;
            component.HugInitiator = user;
            try
            {
                _interaction.InteractHand(user, target.Value);
            }
            finally
            {
                component.HugInitiator = previous;
            }
            return;
        }

        if (_net.IsServer)
        {
            var ev = new RPPanelActionEvent(user, args.Action);
            RaiseLocalEvent(target.Value, ref ev);
        }
    }
}
