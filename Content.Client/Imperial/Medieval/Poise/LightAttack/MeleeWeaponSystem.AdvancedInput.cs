using Content.Client.Gameplay;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Content.Shared.Weapons.Melee;


namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    [Dependency] private readonly AdvancedLightAttackSystem _advanced = default!;
    [Dependency] private readonly SharedHandsSystem _advancedHands = default!;
    private uint _nextAdvancedAttackId; // Allocated once per physical/predicted input.

    private bool TryGetAdvancedCursor(out MapCoordinates point, out EntityCoordinates location)
    {
        point = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
        location = default;
        if (point.MapId == MapId.Nullspace)
            return false;
        if (MapManager.TryFindGridAt(point, out var grid, out _))
            location = TransformSystem.ToCoordinates(grid, point);
        else
            location = TransformSystem.ToCoordinates(_map.GetMap(point.MapId), point);
        return true;
    }

    private void UpdateAdvancedHeldInput(EntityUid user, BoundKeyState primary, BoundKeyState secondary)
    {
        var cursorChecked = false;
        var validCursor = false;
        MapCoordinates point = default;
        EntityCoordinates location = default;
        var targetChecked = false;
        EntityUid? target = null;

        foreach (var uid in _advancedHands.EnumerateHeld(user))
        {
            if (!TryComp<AdvancedLightAttackComponent>(uid, out var component) ||
                !TryComp<MeleeWeaponComponent>(uid, out var melee))
                continue;

            if (component.IsCharged && component.SecondaryHeld && secondary != BoundKeyState.Down)
            {
                // A late release only clears its latch; the server ignores its coordinates.
                if (!cursorChecked && component.Attacker == user && _advanced.IsPending(uid))
                {
                    validCursor = TryGetAdvancedCursor(out point, out location);
                    cursorChecked = true;
                }
                var releaseCoordinates = validCursor ? GetNetCoordinates(location) :
                    component.PendingAttack?.Coordinates ?? GetNetCoordinates(Transform(user).Coordinates);
                RaisePredictiveEvent(new AdvancedChargedAttackReleaseEvent(GetNetEntity(uid), component.AttackId, releaseCoordinates));
                continue;
            }
            var charging = TryComp<ChargedAttackComponent>(uid, out var charge) && charge.CurrentAttacking;

            if (!component.IsCharged && primary != BoundKeyState.Down &&
                (component.PrimaryHeld || component.UsesPrimaryLatch && melee.Attacking && !charging))
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(uid)));

            if (component.Attacker != user || !_advanced.IsPending(uid))
                continue;
            if (!cursorChecked)
            {
                validCursor = TryGetAdvancedCursor(out point, out location);
                cursorChecked = true;
            }
            if (!validCursor)
                continue;

            if (!targetChecked)
            {
                if (_stateManager.CurrentState is GameplayStateBase screen)
                    target = screen.GetClickedEntity(point);
                targetChecked = true;
            }
            var netTarget = GetNetEntity(target);
            var netCoordinates = GetNetCoordinates(location);
            if (component.PendingAttack is { } pending && pending.Target == netTarget && pending.Coordinates.Equals(netCoordinates))
                continue;

            var significant = true;
            if (component.PendingAttack is { } previous && previous.Target == netTarget &&
                TryGetEntity(previous.Coordinates.NetEntity, out _))
            {
                var previousCoordinates = GetCoordinates(previous.Coordinates);
                if (previousCoordinates.IsValid(EntityManager))
                {
                    var previousPoint = TransformSystem.ToMapCoordinates(previousCoordinates);
                    var userPoint = TransformSystem.GetMapCoordinates(user);
                    if (previousPoint.MapId == point.MapId && userPoint.MapId == point.MapId)
                        significant = AdvancedLightAttackAimThrottle.IsSignificant(
                            previousPoint.Position - userPoint.Position, point.Position - userPoint.Position, melee.Range);
                }
            }
            if (!component.ClientAimThrottle.ShouldSend(component.AttackId, Timing.CurTime,
                    component.Strikes, Timing.TickPeriod, significant))
                continue;
            RaisePredictiveEvent(new AdvancedLightAttackAimEvent(GetNetEntity(uid), component.AttackId, netTarget, netCoordinates));
        }
    }
}
