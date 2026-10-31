using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Weapons;

public sealed class MeleeSwingSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MeleeSwingComponent, ItemUnwieldedEvent>(OnUnwielded);
    }

    public bool CanCharge(EntityUid weapon, EntityUid user)
        => TryComp<MeleeSwingComponent>(weapon, out var swing) &&
           (!swing.RequireWielded || TryComp<WieldableComponent>(weapon, out var wieldable) && wieldable.Wielded) &&
           _hands.TryGetActiveItem(user, out var held) && held == weapon;

    public bool TryRelease(EntityUid weapon, EntityUid user, EntityCoordinates aim)
    {
        if (!CanCharge(weapon, user) ||
            !TryComp<ChargedAttackComponent>(weapon, out var charged) || !charged.CurrentAttacking ||
            !TryComp<MeleeWeaponComponent>(weapon, out var melee) ||
            !TryComp<MeleeSwingComponent>(weapon, out var swing) ||
            !_prototypes.TryIndex(swing.Style, out var style) || !MeleeSwingVisuals.IsValid(style) ||
            !TryComp<TransformComponent>(user, out var xform) || xform.MapUid == null ||
            !aim.IsValid(EntityManager))
            return false;

        var target = _transform.ToMapCoordinates(aim);
        if (target.MapId != xform.MapID)
            return false;
        var origin = _transform.GetWorldPosition(xform);
        var direction = target.Position - origin;
        if (direction.LengthSquared() <= 0 || !float.IsFinite(direction.LengthSquared()))
            return false;

        var ratio = MeleeSwingVisuals.ChargeRatio(_timing.CurTime - charged.AttackStart, charged.MaxAttackTime);
        var scale = MeleeSwingVisuals.SwingScale(ratio, swing.MaximumSwingScale);
        if (!_stamina.TryTakeStamina(user, charged.StaminaDamage))
            return false;
        charged.Modifier = 1 + charged.StaticModifier * ratio;
        Dirty(weapon, charged);

        var range = melee.Range;
        var angle = melee.Angle;
        var animation = melee.WideAnimation;
        try
        {
            melee.Range = range * scale;
            melee.Angle = style.Arc;
            melee.WideAnimation = MeleeSwingVisuals.SwingMarker(swing.NextSwingLeftToRight);

            var edge = _transform.ToCoordinates(xform.MapUid.Value,
                new MapCoordinates(origin + direction.Normalized() * melee.Range, xform.MapID));
            var released = _melee.AttemptHeavyAttack(user, weapon, melee, edge); // use the normal melee hit checks
            if (released)
            {
                swing.NextSwingLeftToRight = !swing.NextSwingLeftToRight;        // alternate direction after a successful swing
                Dirty(weapon, swing);
            }
            return released;
        }
        finally
        {
            melee.Range = range;                                                 // restore normal reach after this attack
            melee.Angle = angle;
            melee.WideAnimation = animation;
        }
    }

    private void OnUnwielded(EntityUid uid, MeleeSwingComponent comp, ref ItemUnwieldedEvent args)
    {
        if (TryComp<ChargedAttackComponent>(uid, out var charged) && charged.CurrentAttacking)
            EntityManager.System<ChargedAttackSystem>().StopAttacking(uid, charged, args.User);
    }
}
