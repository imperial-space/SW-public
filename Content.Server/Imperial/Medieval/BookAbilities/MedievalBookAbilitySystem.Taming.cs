using System.Linq;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;

namespace Content.Server.Imperial.Medieval.BookAbilities;

public sealed partial class MedievalBookAbilitySystem
{
    // Matches the targeting radius on ActionBookTaming.
    private const float TamingRange = 6f;

    private sealed class TamingChannel(EntityUid user, EntityUid beast, EntityUid food)
    {
        public readonly EntityUid User = user;
        public readonly EntityUid Beast = beast;
        public readonly EntityUid Food = food;
        public DoAfterId? DoAfter;
        public EntityUid? CancelAction;
    }

    private readonly Dictionary<EntityUid, TamingChannel> _tamingChannels = new();
    private readonly Dictionary<EntityUid, EntityUid> _tamingParticipants = new();

    private void InitializeTaming()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookTamingActionEvent>(OnTamingAction);
        SubscribeLocalEvent<BookTamingLockComponent, BookCancelTamingActionEvent>(OnCancelTamingAction);
        SubscribeLocalEvent<BookTamingLockComponent, BookTamingDoAfterEvent>(OnTamingFinished);
        SubscribeLocalEvent<BookTamingLockComponent, DoAfterAttemptEvent<BookTamingDoAfterEvent>>(OnTamingAttempt);
        SubscribeLocalEvent<BookTamingLockComponent, MoveInputEvent>(OnTamingMoveInput);
        SubscribeLocalEvent<BookTamingLockComponent, DamageChangedEvent>(OnTamingBeastHurt);
        SubscribeLocalEvent<BookTamingLockComponent, MobStateChangedEvent>(OnTamingMobStateChanged);
        SubscribeLocalEvent<BookTamingLockComponent, MindRemovedMessage>(OnTamingMindRemoved);
        // The shared lock system owns Shutdown to refresh predicted movement. Removal is a
        // separate native lifecycle event, raised before the component is deleted on either participant.
        SubscribeLocalEvent<BookTamingLockComponent, ComponentRemove>(OnTamingParticipantRemoved);
        SubscribeLocalEvent<DoAfterComponent, ComponentShutdown>(OnTamingDoAfterShutdown);
    }

    private bool IsFood(EntityUid uid) => HasComp<EdibleComponent>(uid) || HasComp<FoodComponent>(uid);

    private void OnTamingAction(EntityUid uid, LearnedKnowledgeComponent comp, BookTamingActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookTaming")) return;
        var food = HeldRightFirst(uid).Where(IsFood).Select(item => (EntityUid?) item).FirstOrDefault();
        if (food is not { } heldFood)
        {
            AbilityError(uid, "book-portable-taming-need-food");
            return;
        }
        var failure = TamingFailure(uid, args.Target);
        if (failure != null)
        {
            AbilityError(uid, failure);
            return;
        }
        if (!StartTaming(uid, args.Target, heldFood))
        {
            AbilityError(uid, "book-portable-taming-cannot-start");
            return;
        }
        // Native action handling starts UseDelay now, including if this channel is later cancelled.
        args.Handled = true;
    }

    private string? TamingFailure(EntityUid user, EntityUid beast)
    {
        if (_tamingParticipants.ContainsKey(user) || _tamingParticipants.ContainsKey(beast))
            return "book-portable-taming-busy";
        if (TryComp<MedievalCompanionComponent>(beast, out var companion) && companion.Master == user)
            return "book-portable-taming-already-owned";
        if (!_companions.CanTame(beast, user))
            return "book-portable-taming-invalid-target";
        if (_companions.OwnedCount(user) >= 1)
            return "book-portable-taming-limit";
        if (!_interaction.InRangeAndAccessible(user, beast, TamingRange))
            return "book-portable-taming-too-far";
        if (!TryComp<DamageableComponent>(beast, out var damage) || damage.TotalDamage < 20)
            return "book-portable-taming-not-weakened";
        return null;
    }

    private bool StartTaming(EntityUid user, EntityUid beast, EntityUid food)
    {
        var channel = new TamingChannel(user, beast, food);
        _tamingChannels.Add(user, channel);
        _tamingParticipants.Add(user, user);
        _tamingParticipants.Add(beast, user);
        AddComp<BookTamingLockComponent>(user);
        AddComp<BookTamingLockComponent>(beast);
        _physics.SetLinearVelocity(user, Vector2.Zero);
        _physics.SetLinearVelocity(beast, Vector2.Zero);
        _companions.Pacify(beast, TimeSpan.FromSeconds(35));
        _actions.AddAction(user, ref channel.CancelAction, "ActionBookCancelTaming");

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, 30,
                new BookTamingDoAfterEvent(), user, beast, food)
            {
                // Movement input explicitly cancels below. Small collision/network corrections of
                // either body must not predict a false cancellation; native range checks still apply.
                BreakOnMove = false,
                BreakOnDamage = true,
                DistanceThreshold = TamingRange,
                NeedHand = true,
                BreakOnHandChange = false,
                BreakOnDropItem = false,
                RequireCanInteract = true,
                CancelDuplicate = false,
                AttemptFrequency = AttemptFrequency.EveryTick,
            }, out var id))
        {
            ReleaseTaming(user, false);
            return false;
        }
        channel.DoAfter = id;
        if (_tamingChannels.ContainsKey(user))
            _popup.PopupEntity(Loc.GetString("book-portable-taming-started"), user, user);
        return true;
    }

    private bool CanContinueTaming(TamingChannel channel) =>
        !TerminatingOrDeleted(channel.Beast) && !TerminatingOrDeleted(channel.Food) &&
        HasComp<BookTamingLockComponent>(channel.Beast) && Knows(channel.User, "BookTaming") &&
        _hands.IsHolding(channel.User, channel.Food, out _) && IsFood(channel.Food) &&
        _companions.CanTame(channel.Beast, channel.User);

    private void OnTamingAttempt(EntityUid uid, BookTamingLockComponent comp,
        DoAfterAttemptEvent<BookTamingDoAfterEvent> args)
    {
        if (!_tamingChannels.TryGetValue(uid, out var channel) || !CanContinueTaming(channel))
            args.Cancel();
    }

    private void OnCancelTamingAction(EntityUid uid, BookTamingLockComponent comp, BookCancelTamingActionEvent args)
    {
        if (args.Handled || !_tamingChannels.ContainsKey(uid)) return;
        args.Handled = true;
        CancelTaming(uid);
    }

    private void OnTamingMoveInput(EntityUid uid, BookTamingLockComponent comp, ref MoveInputEvent args)
    {
        // NPC navigation also produces movement inputs. Only the player's input is a voluntary cancellation.
        var pressed = args.Entity.Comp.HeldMoveButtons & ~args.OldMovement & MoveButtons.AnyDirection;
        if (_tamingChannels.ContainsKey(uid) && pressed != MoveButtons.None)
            CancelTaming(uid);
    }

    private void OnTamingMobStateChanged(EntityUid uid, BookTamingLockComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive && _tamingParticipants.TryGetValue(uid, out var user))
            CancelTaming(user);
    }

    private void OnTamingBeastHurt(EntityUid uid, BookTamingLockComponent comp, DamageChangedEvent args)
    {
        // DoAfter already checks the trainer. Apply its standard damage rule (default threshold: 1) to the beast too.
        if (args.InterruptsDoAfters && args.DamageDelta is { } delta && delta.GetTotal() >= 1 &&
            _tamingParticipants.TryGetValue(uid, out var user) && uid != user)
            CancelTaming(user);
    }

    private void OnTamingMindRemoved(EntityUid uid, BookTamingLockComponent comp, MindRemovedMessage args)
    {
        if (_tamingParticipants.TryGetValue(uid, out var user))
            CancelTaming(user);
    }

    private void OnTamingParticipantRemoved(EntityUid uid, BookTamingLockComponent comp, ComponentRemove args)
    {
        if (_tamingParticipants.TryGetValue(uid, out var user))
            CancelTaming(user);
    }

    private void OnTamingDoAfterShutdown(EntityUid uid, DoAfterComponent comp, ComponentShutdown args)
    {
        if (_tamingChannels.ContainsKey(uid))
            CancelTaming(uid);
    }

    private void OnTamingFinished(EntityUid uid, BookTamingLockComponent comp, BookTamingDoAfterEvent args)
    {
        if (args.Handled || !_tamingChannels.TryGetValue(uid, out var channel) ||
            args.Target != channel.Beast || args.Used != channel.Food ||
            channel.DoAfter is { } id && id != args.DoAfter.Id) return;
        args.Handled = true;
        var success = !args.Cancelled && CanContinueTaming(channel) &&
            _interaction.InRangeAndAccessible(uid, channel.Beast, TamingRange) && _companions.Tame(channel.Beast, uid);
        ReleaseTaming(uid, !success);
        if (success)
        {
            QueueDel(channel.Food);
            _popup.PopupEntity(Loc.GetString("book-portable-taming-success"), uid, uid);
        }
    }

    private void CancelTaming(EntityUid user)
    {
        if (!_tamingChannels.TryGetValue(user, out var channel)) return;
        // Release before cancelling: native DoAfter.Cancel raises its result synchronously.
        ReleaseTaming(user, true);
        if (_doAfter.IsRunning(channel.DoAfter))
            _doAfter.Cancel(channel.DoAfter);
    }

    private void ReleaseTaming(EntityUid user, bool retaliate)
    {
        if (!_tamingChannels.Remove(user, out var channel)) return;
        _tamingParticipants.Remove(channel.User);
        _tamingParticipants.Remove(channel.Beast);
        if (channel.CancelAction is { } action && !TerminatingOrDeleted(action))
        {
            _actions.RemoveAction(action);
            QueueDel(action);
        }
        foreach (var participant in new[] { channel.User, channel.Beast })
        {
            if (TryComp<BookTamingLockComponent>(participant, out var locked) && locked.LifeStage < ComponentLifeStage.Stopping)
                RemComp<BookTamingLockComponent>(participant);
        }
        if (!TerminatingOrDeleted(channel.Beast))
            _companions.ReleaseTraining(channel.Beast,
                retaliate && !TerminatingOrDeleted(user) ? user : null);
        if (retaliate && !TerminatingOrDeleted(user))
            _popup.PopupEntity(Loc.GetString("book-portable-taming-cancelled"), user, user);
    }

    private void CleanupTaming()
    {
        foreach (var channel in _tamingChannels.Values.ToArray())
        {
            ReleaseTaming(channel.User, false);
            if (_doAfter.IsRunning(channel.DoAfter))
                _doAfter.Cancel(channel.DoAfter);
        }
        _tamingParticipants.Clear();
    }
}
