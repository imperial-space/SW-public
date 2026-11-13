using Content.Shared.Camera;
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Handles server-side lock presentation: private Target warnings and bounded camera streaming.</summary>
public sealed class MeleeLockOnPvsSystem : EntitySystem
{
    [Dependency] private readonly MeleeLockOnSystem _lockOn = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeLockOnComponent, ExpandPvsEvent>(OnExpandPvs);
        SubscribeLocalEvent<MeleeLockOnComponent, MeleeLockOnChangedEvent>(OnLockChanged);
        SubscribeLocalEvent<MeleeLockOnComponent, GetEyePvsScaleEvent>(OnPvsScale);
    }

    private void OnExpandPvs(EntityUid user, MeleeLockOnComponent comp, ref ExpandPvsEvent args)
    {
        if (comp.Target is not { } target || TerminatingOrDeleted(target))
            return;

        // The engine still applies its visibility mask; unlock needs no persistent override cleanup.
        args.Entities ??= new List<EntityUid>();
        args.Entities.Add(target);
    }

    private void OnLockChanged(EntityUid user, MeleeLockOnComponent comp, ref MeleeLockOnChangedEvent args)
    {
        if (!TerminatingOrDeleted(user) && HasComp<ContentEyeComponent>(user) && HasComp<EyeComponent>(user))
            _eye.UpdatePvsScale(user);

        if (comp.Target is not { } target || TerminatingOrDeleted(target) ||
            !TryComp<MeleeLockOnTrackedTargetComponent>(target, out var tracking) ||
            !TryComp<ActorComponent>(target, out var actor) || actor.PlayerSession.AttachedEntity != target ||
            _timing.CurTime < tracking.NextHostileIntentAt)
            return;

        // Retain this timestamp on the Target after its last watcher unlocks.
        // A suppressed warning does not extend the cooldown.
        tracking.NextHostileIntentAt = _timing.CurTime + TimeSpan.FromTicks(Math.Max(0, tracking.HostileIntentCooldown.Ticks));
        _popup.PopupEntity(Loc.GetString("medieval-melee-lock-on-hostile-intent"), target, actor.PlayerSession, PopupType.MediumCaution);
    }

    private void OnPvsScale(EntityUid user, MeleeLockOnComponent comp, ref GetEyePvsScaleEvent args)
    {
        if (comp.Target != null && _lockOn.CanFollowCamera(user))
            args.Scale += Math.Clamp(MathF.Min(comp.MaxCameraPanFraction,
                (comp.FirstDistanceThreshold + comp.CameraFollowRangeExtension - comp.CameraPanStartThreshold) * comp.CameraPanMultiplier),
                0f, 0.999f);
    }
}
