using System;
using System.Numerics;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

public sealed partial class AdvancedLightAttackSystem
{
    private void UpdateScheduledPresentation(EntityUid weapon, AdvancedLightAttackComponent component)
    {
        if (component.PresentationState != AdvancedMeleeVisualState.Scheduled)
            return;
        component.PresentationStrike = component.Strikes;
        if (component.PendingAttack is { } attack)
            CaptureScheduledPresentation(weapon, component, attack.Coordinates);
    }

    private void CaptureScheduledPresentation(EntityUid weapon, AdvancedLightAttackComponent component,
        Robust.Shared.Map.NetCoordinates aim)
    {
        if (component.Attacker is not { } user || !TryComp<MeleeWeaponComponent>(weapon, out var melee) ||
            !TryGetEntity(aim.NetEntity, out _) || !GetCoordinates(aim).IsValid(EntityManager))
            return;
        var target = _transform.ToMapCoordinates(GetCoordinates(aim));
        if (target.MapId != Transform(user).MapID)
            return;
        var offset = target.Position - _transform.GetWorldPosition(user);
        // Match DoLungeAnimation's existing visual reach buffer.
        var length = MathF.Max(0f, melee.Range - 0.2f);
        if (offset.LengthSquared() > length * length && offset.LengthSquared() > 0f)
            offset = Vector2.Normalize(offset) * length;
        component.Presentation = Snapshot(melee, offset, melee.Angle,
            component.IsCharged ? melee.WideAnimation.Id : melee.Animation.Id, confirmed: false);
    }

    private static AdvancedMeleeVisualData Snapshot(MeleeWeaponComponent melee, Vector2 worldOffset,
        Angle angle, string? animation, bool confirmed)
        => new()
        {
            WorldOffset = worldOffset,
            Angle = angle,
            Animation = animation,
            SwingLeft = !confirmed && melee.CPSwingBeverage ? !melee.SwingLeft : melee.SwingLeft,
            SpriteRotation = melee.WideAnimationRotation,
            Length = melee.CPAnimationLength,
            Offset = melee.CPAnimationOffset,
        };

    /// <summary>
    /// Server-only routing hook reached after normal attack checks produce an
    /// actual lunge. Returning true suppresses its duplicate buffered notification.
    /// The owner continues through the existing predicted DoLunge path.
    /// </summary>
    public bool TryPresentAdvancedSwing(EntityUid user, EntityUid weapon, Angle angle, Vector2 localOffset, string? animation)
    {
        if (!_net.IsServer || !TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            component.Attacker != user || component.Phase != AdvancedLightAttackPhase.Recovery ||
            component.PresentationState != AdvancedMeleeVisualState.Scheduled ||
            !TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return false;
        var parent = Transform(user).ParentUid;
        var worldOffset = parent.IsValid() ? _transform.GetWorldRotation(parent).RotateVec(localOffset) : localOffset;
        component.Presentation = Snapshot(melee, worldOffset, angle, animation, confirmed: true);
        component.PresentationStrike = _timing.CurTime;
        component.PresentationState = AdvancedMeleeVisualState.Confirmed;
        Publish(weapon, component);
        return true;
    }

    /// <summary>Consumed commitments that never produced a lunge must cancel their visual prediction.</summary>
    public void FinishPresentation(EntityUid user, EntityUid weapon, uint attackId)
    {
        if (!_net.IsServer || !TryComp<AdvancedLightAttackComponent>(weapon, out var component) ||
            component.Attacker != user || component.AttackId != attackId ||
            component.PresentationState != AdvancedMeleeVisualState.Scheduled)
            return;
        component.PresentationState = AdvancedMeleeVisualState.Cancelled;
        Publish(weapon, component);
    }
}
