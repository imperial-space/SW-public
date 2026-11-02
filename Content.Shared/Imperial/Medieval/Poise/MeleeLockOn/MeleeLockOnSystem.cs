using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Imperial.TargetOverlay.Events;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>
/// Persistent melee lock. All cursor picking is delegated to projectile TargetOverlay;
/// no secondary target-detection loop and no actor polling.
/// </summary>
public sealed class MeleeLockOnSystem : EntitySystem
{
    private const string ActionProto = "ActionMeleeLockOnToggle";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeLockOnComponent, MeleeLockOnToggleEvent>(OnToggle);
        SubscribeLocalEvent<TargetOverlayStartedEvent>(OnTargetOverlayStarted);
        SubscribeLocalEvent<TargetOverlayStoppedEvent>(OnTargetOverlayStopped);
        SubscribeAllEvent<MeleeLockOnTargetEvent>(OnTarget);
        SubscribeLocalEvent<MeleeLockOnTrackedTargetComponent, MobStateChangedEvent>(OnTrackedMobStateChanged);
        SubscribeLocalEvent<MeleeLockOnTrackedTargetComponent, ComponentShutdown>(OnTrackedTargetShutdown);
    }

    // Hooked into CombatMode's EXISTING lifecycle, never duplicate subscriptions.
    public void OnCombatInitialized(EntityUid user)
    {
        if (_net.IsClient)
            return;
        var comp = EnsureComp<MeleeLockOnComponent>(user);
        if (comp.ActionEntity != null)
            return;
        _actions.AddAction(user, ref comp.ActionEntity, ActionProto);
        Dirty(user, comp);
    }

    public void OnCombatShutdown(EntityUid user)
    {
        if (!TryComp<MeleeLockOnComponent>(user, out var comp))
            return;
        if (comp.TargetingMode)
            SetMode(user, comp, false);
        Clear(user, comp);
        if (_net.IsServer)
        {
            _actions.RemoveAction(user, comp.ActionEntity);
            comp.ActionEntity = null;
            Dirty(user, comp);
        }
    }

    public void OnCombatModeChanged(EntityUid user, bool entering)
    {
        if (!TryComp<MeleeLockOnComponent>(user, out var comp))
            return;
        if (entering)
            SetMode(user, comp, true);
        else
        {
            SetMode(user, comp, false);
            Clear(user, comp);
        }
    }

    private void OnToggle(EntityUid uid, MeleeLockOnComponent comp, MeleeLockOnToggleEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        // Existing ActionsSystem handles its own toggled bit when requested.
        SetMode(uid, comp, !comp.TargetingMode);
    }

    private void SetMode(EntityUid user, MeleeLockOnComponent comp, bool enabled)
    {
        // Entering combat or using the hotbar must not take input from a ranged spell.
        if (enabled && comp.RangedTargetingActive)
            return;
        if (comp.TargetingMode == enabled)
            return;
        comp.TargetingMode = enabled;
        Dirty(user, comp);
        if (comp.ActionEntity != null)
            _actions.SetToggled(comp.ActionEntity, enabled);
    }

    private void OnTargetOverlayStarted(TargetOverlayStartedEvent args)
    {
        if (!TryComp<MeleeLockOnComponent>(args.User, out var comp) ||
            (args.Sender != null && args.Sender == comp.ActionEntity))
            return;
        if (!comp.RangedTargetingActive)
        {
            comp.RangedTargetingActive = true;
            Dirty(args.User, comp);
        }
        if (comp.TargetingMode)
            SetMode(args.User, comp, false);
        Clear(args.User, comp);
    }

    private void OnTargetOverlayStopped(TargetOverlayStoppedEvent args)
    {
        if (!TryComp<MeleeLockOnComponent>(args.User, out var comp) || !comp.RangedTargetingActive)
            return;
        comp.RangedTargetingActive = false;
        Dirty(args.User, comp);
    }

    private void OnTarget(MeleeLockOnTargetEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user ||
            !TryComp<MeleeLockOnComponent>(user, out var comp))
            return;
        if (args.Target is not { } netTarget)
        {
            Clear(user, comp);
            if (args.ExitTargeting && comp.TargetingMode)
                SetMode(user, comp, false);
            return;
        }
        // Also reject a queued melee selection if ranged targeting started first.
        if (comp.RangedTargetingActive)
            return;
        if (!TryGetEntity(netTarget, out var target) || target is not { } uid ||
            uid == user || TerminatingOrDeleted(uid) || !HasComp<DamageableComponent>(uid) || Transform(uid).MapID != Transform(user).MapID ||
            !_interaction.InRangeUnobstructed(user, uid,
                SharedInteractionSystem.MaxRaycastRange, overlapCheck: false))
            return;
        if (comp.Target != uid)
        {
            Clear(user, comp);
            comp.Target = uid;
            Dirty(user, comp);
            if (_net.IsServer)
                EnsureComp<MeleeLockOnTrackedTargetComponent>(uid).Watchers.Add(user);
        }
        // A successful selection exits selection mode, not combat mode.
        if (comp.TargetingMode)
            SetMode(user, comp, false);
    }

    public void Clear(EntityUid user, MeleeLockOnComponent? comp = null)
    {
        if (!Resolve(user, ref comp, false) || comp.Target is not { } previous)
            return;
        comp.Target = null;
        Dirty(user, comp);
        if (_net.IsServer && TryComp<MeleeLockOnTrackedTargetComponent>(previous, out var tracker))
            tracker.Watchers.Remove(user);
    }

    public EntityUid? GetTarget(EntityUid user)
    {
        if (!TryComp<MeleeLockOnComponent>(user, out var comp) || comp.Target is not { } target)
            return null;
        if (Exists(target) && !TerminatingOrDeleted(target) && HasComp<DamageableComponent>(target) &&
            Transform(target).MapID == Transform(user).MapID)
            return target;
        if (_net.IsServer)
            Clear(user, comp);
        return null;
    }

    private void OnTrackedMobStateChanged(EntityUid target,
        MeleeLockOnTrackedTargetComponent tracker, MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState != MobState.Dead)
            return;
        // A living target dying invalidates the existing lock. Selecting a
        // *previously dead* body again remains allowed by OnTarget.
        ClearWatchers(target, tracker);
    }

    private void OnTrackedTargetShutdown(EntityUid target,
        MeleeLockOnTrackedTargetComponent tracker, ComponentShutdown args)
    {
        if (_net.IsClient)
            return;
        ClearWatchers(target, tracker);
    }

    private void ClearWatchers(EntityUid target, MeleeLockOnTrackedTargetComponent tracker)
    {
        foreach (var user in new List<EntityUid>(tracker.Watchers))
        {
            if (TryComp<MeleeLockOnComponent>(user, out var lockOn) && lockOn.Target == target)
                Clear(user, lockOn);
        }
        tracker.Watchers.Clear();
    }

    public bool IsTargeting(EntityUid user)
        => TryComp<MeleeLockOnComponent>(user, out var comp) && comp.TargetingMode;
}
