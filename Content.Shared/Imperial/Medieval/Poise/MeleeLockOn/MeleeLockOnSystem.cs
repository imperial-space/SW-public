using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Imperial.TargetOverlay.Events;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>
/// Persistent melee lock. All cursor picking is delegated to projectile TargetOverlay;
/// no secondary target-detection loop. Tracking updates only currently locked users.
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
        SubscribeAllEvent<MeleeLockOnViewEvent>(OnViewReport);
        SubscribeLocalEvent<MeleeLockOnComponent, ComponentShutdown>(OnLockOnShutdown);
        SubscribeLocalEvent<MeleeLockOnComponent, EntityUnpausedEvent>(OnLockOnUnpaused);
    }

    // Hooked into CombatMode's EXISTING lifecycle, never duplicate subscriptions.
    public void OnCombatInitialized(EntityUid user)
    {
        if (_net.IsClient)
            return;
        // Keep values inherited from the race's mob prototype, including its lock range.
        var comp = EnsureComp<MeleeLockOnComponent>(user);
        if (comp.Target == null)
            ResetLockOnTimer(comp);
        if (comp.ActionEntity != null)
            return;
        _actions.AddAction(user, ref comp.ActionEntity, ActionProto);
        DirtyFields(user, comp, null, nameof(comp.ActionEntity), nameof(comp.LockOnTimeRemaining), nameof(comp.LockOnExpiresAt));
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
            DirtyField(user, comp, nameof(comp.ActionEntity));
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
        DirtyField(user, comp, nameof(comp.TargetingMode));
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
            DirtyField(args.User, comp, nameof(comp.RangedTargetingActive));
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
        DirtyField(args.User, comp, nameof(comp.RangedTargetingActive));
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
        if (_net.IsServer)
        {
            if (comp.Target == null)
                ResetLockOnTimer(comp);
            else
                AdvanceLockOnTimer(user, comp, _timing.CurTime, forceSync: true);
            if (comp.Target != null && comp.LockOnTimeRemaining <= TimeSpan.Zero)
                return;
        }
        if (comp.Target != uid)
        {
            // A replacement is one continuous lock: no null Target, timer refill,
            // Active-component removal, or intermediate unlock notification.
            if (_net.IsServer && comp.Target is { } previous &&
                TryComp<MeleeLockOnTrackedTargetComponent>(previous, out var previousTracker))
                previousTracker.Watchers.Remove(user);
            comp.Target = uid;
            comp.LockGeneration++;
            if (_net.IsServer)
            {
                EnsureComp<MeleeLockOnTrackedTargetComponent>(uid).Watchers.Add(user);
                var alreadyTracking = HasComp<ActiveMeleeLockOnComponent>(user);
                var tracking = EnsureComp<ActiveMeleeLockOnComponent>(user);
                if (!alreadyTracking)
                    tracking.ViewHalfExtents = MaximumViewHalfExtents(user);
                tracking.LastReport = _timing.CurTime;
                tracking.ReportedVisible = false;
                tracking.HasViewReport = false;
                tracking.Unoccluded = false;
                tracking.NextVisibilityCheck = TimeSpan.Zero;
                comp.LockOnTimerLastUpdated = _timing.CurTime;
                SetLockOnTimerCondition(user, comp, true, _timing.CurTime);
                var changed = new MeleeLockOnChangedEvent();
                RaiseLocalEvent(user, ref changed);
            }
            DirtyFields(user, comp, null, nameof(comp.Target), nameof(comp.LockGeneration),
                nameof(comp.LockOnTimeRemaining), nameof(comp.LockOnExpiresAt));
        }
        // A successful selection exits selection mode, not combat mode.
        if (comp.TargetingMode)
            SetMode(user, comp, false);
    }

    public void Clear(EntityUid user, MeleeLockOnComponent? comp = null)
    {
        if (!Resolve(user, ref comp, false) || comp.Target is not { } previous)
            return;
        if (_net.IsServer)
        {
            ResetLockOnTimer(comp);
        }
        comp.Target = null;
        DirtyFields(user, comp, null, nameof(comp.Target), nameof(comp.LockOnTimeRemaining), nameof(comp.LockOnExpiresAt));
        if (_net.IsServer)
        {
            if (TryComp<MeleeLockOnTrackedTargetComponent>(previous, out var tracker))
                tracker.Watchers.Remove(user);
            RemComp<ActiveMeleeLockOnComponent>(user);
        }
        var changed = new MeleeLockOnChangedEvent();
        RaiseLocalEvent(user, ref changed);
    }

    public EntityUid? GetTarget(EntityUid user)
    {
        if (!TryComp<MeleeLockOnComponent>(user, out var comp))
            return null;
        if (comp.Target is not { } target)
            return null;
        if (Exists(target) && !TerminatingOrDeleted(target) && HasComp<DamageableComponent>(target) &&
            Transform(target).MapID != MapId.Nullspace &&
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

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    // A stopped or detached client must not keep a lock alive indefinitely.
    public static readonly TimeSpan ViewReportLifetime = TimeSpan.FromSeconds(0.75);
    private static readonly TimeSpan VisibilityCheckInterval = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan LockOnTimerSyncInterval = TimeSpan.FromSeconds(0.25);

    private void AdvanceLockOnTimer(EntityUid user, MeleeLockOnComponent comp, TimeSpan now, bool forceSync = false)
    {
        var remaining = MeleeLockOnMath.AdvanceLockOnTime(comp.LockOnTimeRemaining, comp.LockOnTimerLastUpdated, now,
            comp.IsLockOnTimeDraining, comp.LockOnRecoveryStartedAt, comp.LockOnTimeout, comp.LockOnRecoveryDelay, comp.LockOnRecoveryRate);
        var changed = remaining != comp.LockOnTimeRemaining;
        comp.LockOnTimeRemaining = remaining;
        comp.LockOnTimerLastUpdated = now;
        if (changed && (forceSync || now >= comp.NextLockOnTimerSync))
        {
            comp.NextLockOnTimerSync = now + LockOnTimerSyncInterval;
            DirtyField(user, comp, nameof(comp.LockOnTimeRemaining));
        }
    }

    private void SetLockOnTimerCondition(EntityUid user, MeleeLockOnComponent comp, bool draining, TimeSpan now)
    {
        if (draining)
        {
            comp.IsLockOnTimeDraining = true;
            comp.LockOnRecoveryStartedAt = null;
        }
        else
        {
            if (comp.IsLockOnTimeDraining || comp.LockOnRecoveryStartedAt == null)
                comp.LockOnRecoveryStartedAt = now;
            comp.IsLockOnTimeDraining = false;
        }

        TimeSpan? deadline = draining ? now + comp.LockOnTimeRemaining : null;
        if (comp.LockOnExpiresAt != deadline)
        {
            comp.LockOnExpiresAt = deadline;
            DirtyFields(user, comp, null, nameof(comp.LockOnExpiresAt), nameof(comp.LockOnTimeRemaining));
        }
    }

    private static void ResetLockOnTimer(MeleeLockOnComponent comp)
    {
        comp.LockOnTimeRemaining = TimeSpan.FromTicks(Math.Max(0, comp.LockOnTimeout.Ticks));
        comp.LockOnTimerLastUpdated = null;
        comp.LockOnRecoveryStartedAt = null;
        comp.IsLockOnTimeDraining = false;
        comp.LockOnExpiresAt = null;
        comp.NextLockOnTimerSync = TimeSpan.Zero;
    }

    private void OnLockOnShutdown(EntityUid user, MeleeLockOnComponent component, ComponentShutdown args)
    {
        Clear(user, component);
    }

    private void OnLockOnUnpaused(EntityUid user, MeleeLockOnComponent comp, ref EntityUnpausedEvent args)
    {
        // Keep all absolute clocks paused, and mark the networked deadline
        // explicitly so a preceding timer-only delta cannot hide its new value.
        if (comp.LockOnTimerLastUpdated is { } updated)
            comp.LockOnTimerLastUpdated = updated + args.PausedTime;
        if (comp.LockOnRecoveryStartedAt is { } recovery)
            comp.LockOnRecoveryStartedAt = recovery + args.PausedTime;
        comp.NextLockOnTimerSync += args.PausedTime;
        if (comp.LockOnExpiresAt is not { } deadline)
            return;
        comp.LockOnExpiresAt = deadline + args.PausedTime;
        if (_net.IsServer)
            DirtyField(user, comp, nameof(comp.LockOnExpiresAt));
    }

    /// <summary>The PoorVision trait uses PermanentBlindness, including when glasses correct it.</summary>
    public bool CanFollowCamera(EntityUid user)
        => !HasComp<PermanentBlindnessComponent>(user) &&
           !(TryComp<BlindableComponent>(user, out var blind) && blind.IsBlind);

    private Vector2 MaximumViewHalfExtents(EntityUid user)
    {
        var zoom = Vector2.One;
        if (TryComp<ContentEyeComponent>(user, out var content))
            zoom = content.TargetZoom;

        // These match the existing viewport limits: configurable width and 15 tiles high.
        return new Vector2(_configuration.GetCVar(CCVars.ViewportMaximumWidth) / 2f, 7.5f) * zoom;
    }

    private void OnViewReport(MeleeLockOnViewEvent args, EntitySessionEventArgs session)
    {
        if (!_net.IsServer || session.SenderSession.AttachedEntity is not { } user ||
            !TryComp<MeleeLockOnComponent>(user, out var comp) ||
            !TryComp<ActiveMeleeLockOnComponent>(user, out var tracking) ||
            comp.Target is not { } target || comp.LockGeneration != args.Generation ||
            GetNetEntity(target) != args.Target || !MeleeLockOnMath.ValidHalfExtents(args.HalfExtents))
            return;

        // Ignore stale reports for old targets/generations, and never let a client enlarge
        // its range beyond the viewport dimensions and zoom authorized by the server.
        tracking.ViewHalfExtents = Vector2.Min(args.HalfExtents, MaximumViewHalfExtents(user));
        tracking.LastReport = _timing.CurTime;
        tracking.HasViewReport = true;
        tracking.ReportedVisible = args.Visible;
    }

    private Angle TrackingRotation(EntityUid user)
    {
        if (TryComp<InputMoverComponent>(user, out var mover))
            return -_mover.GetParentGridAngle(mover);

        var xform = Transform(user);
        var relative = xform.GridUid ?? xform.MapUid;
        return relative is { } parent ? -_transform.GetWorldRotation(parent) : Angle.Zero;
    }

    private float TrackingViewFraction(EntityUid user, EntityUid target, ActiveMeleeLockOnComponent tracking)
    {
        var extents = Vector2.Min(tracking.ViewHalfExtents, MaximumViewHalfExtents(user));
        var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(user);
        return MeleeLockOnMath.ViewFraction(delta, extents, TrackingRotation(user));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<MeleeLockOnComponent, ActiveMeleeLockOnComponent>();
        while (query.MoveNext(out var user, out var comp, out var tracking))
        {
            // Distance never clears a lock immediately. Only invalid entities/maps do.
            if (GetTarget(user) is not { } target)
                continue;

            var now = _timing.CurTime;
            AdvanceLockOnTimer(user, comp, now);
            var visible = tracking.HasViewReport && tracking.ReportedVisible && now - tracking.LastReport <= ViewReportLifetime;
            if (visible)
            {
                if (now >= tracking.NextVisibilityCheck)
                {
                    tracking.NextVisibilityCheck = now + VisibilityCheckInterval;
                    tracking.Unoccluded =
                        !(TryComp<BlindableComponent>(user, out var blind) && blind.IsBlind) &&
                        (MetaData(target).Flags & MetaDataFlags.InContainer) == 0 &&
                        !(TryComp<VisibilityComponent>(target, out var visibility) &&
                          TryComp<EyeComponent>(user, out var eye) && (visibility.Layer & eye.VisibilityMask) == 0) &&
                        _examine.InRangeUnOccluded(user, target, 0,
                            predicate: entity => entity == user || entity == target);
                }
                visible = tracking.Unoccluded;
            }

            var draining = MeleeLockOnMath.LockAtRisk(visible, TrackingViewFraction(user, target, tracking), comp.FirstDistanceThreshold);
            SetLockOnTimerCondition(user, comp, draining, now);
            if (draining && comp.LockOnTimeRemaining <= TimeSpan.Zero)
                Clear(user, comp);
        }
    }
}
