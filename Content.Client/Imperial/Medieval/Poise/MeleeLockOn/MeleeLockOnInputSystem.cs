using Content.Client.Imperial.TargetOverlay;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;
using Content.Shared.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>
/// Melee-only input. Uses the existing projectile TargetOverlay cursor scan;
/// no separate entity scanner, spell target list, or update polling.
/// </summary>
public sealed class MeleeLockOnInputSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MeleeLockOnSystem _lockOn = default!;
    [Dependency] private readonly TargetOverlaySystem _projectileOverlay = default!;
    [Dependency] private readonly Content.Client.Weapons.Melee.MeleeWeaponSystem _meleeInput = default!;

    private TimeSpan _lastQuickPress = TimeSpan.MinValue;
    private static readonly TimeSpan DoubleTap = TimeSpan.FromMilliseconds(300);

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnLeftClick), typeof(ActionUIController))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnRightClick))
            .Bind(ContentKeyFunctions.MedievalMeleeQuickTarget, InputCmdHandler.FromDelegate(OnQuickTarget))
            .Register<MeleeLockOnInputSystem>();
    }

    private bool OnLeftClick(ICommonSession? session, EntityCoordinates coordinates, EntityUid entity)
    {
        if (!_timing.IsFirstTimePredicted || session?.AttachedEntity is not { } user ||
            !_lockOn.IsTargeting(user) || _projectileOverlay.IsRangedTargetingDisplayed)
            return false;

        _meleeInput.SuppressTargetingMousePress();
        if (_projectileOverlay.PickDamageableUnderCursor() is { } target)
            RaisePredictiveEvent(new MeleeLockOnTargetEvent(GetNetEntity(target)));
        return true;
    }

    private bool OnRightClick(ICommonSession? session, EntityCoordinates coordinates, EntityUid entity)
    {
        if (!_timing.IsFirstTimePredicted || session?.AttachedEntity is not { } user ||
            !_lockOn.IsTargeting(user) || _projectileOverlay.IsRangedTargetingDisplayed)
            return false;

        _meleeInput.SuppressTargetingMousePress();
        RaisePredictiveEvent(new MeleeLockOnTargetEvent(null, exitTargeting: true));
        return true;
    }

    private void OnQuickTarget(ICommonSession? session)
    {
        if (!_timing.IsFirstTimePredicted || session?.AttachedEntity is not { } user ||
            !HasComp<MeleeLockOnComponent>(user))
            return;

        // A ranged spell owns targeting while its existing overlay is displayed.
        if (_projectileOverlay.IsRangedTargetingDisplayed)
            return;

        var now = _timing.CurTime;
        if (_lastQuickPress != TimeSpan.MinValue && now - _lastQuickPress <= DoubleTap)
        {
            _lastQuickPress = TimeSpan.MinValue;
            RaisePredictiveEvent(new MeleeLockOnTargetEvent(null));
            return;
        }

        // First press acts immediately; no 300 ms input delay.
        _lastQuickPress = now;
        if (_projectileOverlay.PickDamageableUnderCursor() is { } target)
            RaisePredictiveEvent(new MeleeLockOnTargetEvent(GetNetEntity(target)));
    }
}
