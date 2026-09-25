using Content.Shared.Imperial.Medieval.Skills;
using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Wieldable;
using Content.Shared.TDMNaming.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

public sealed partial class SkillPerkSystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedWieldableSystem _wield = default!;

    private void RefreshStrengthEquipment(EntityUid uid, SkillGrantedActionsComponent state)
    {
        if (!TryComp<SkillsComponent>(uid, out var skills))
            return;
        var level = SkillScaling.Level(skills, SharedSkillsSystem.StrengthId);
        var previous = state.AppliedStrength;
        state.AppliedStrength = level;
        if (level >= previous)
            return;
        // Re-wielding will allocate the newly required hands; never leave a free-hand bonus behind.
        if (previous >= SkillScaling.Master && level < SkillScaling.Master
            || previous >= SkillScaling.Expert && level < SkillScaling.Expert
            || level < SkillScaling.Basic)
            _wield.UnwieldAll(uid, force: true);
        if (level < SkillScaling.Master && _inventory.TryGetSlotEntity(uid, "belt", out var belt)
            && HasComp<SkillBeltBackpackComponent>(belt))
            _inventory.TryUnequip(uid, "belt", silent: true, force: true);
        if (level < SkillScaling.Legendary)
            foreach (var held in _hands.EnumerateHeld(uid).ToArray())
                if (HasComp<SkillHeavyCargoComponent>(held))
                    _hands.TryDrop(uid, held, checkActionBlocker: false);
    }

    private void InitializeStrength() => SubscribeLocalEvent<SkillsComponent, MeleeDamageDealtEvent>(OnMeleeLanded);

    private void OnMeleeLanded(EntityUid uid, SkillsComponent skills, ref MeleeDamageDealtEvent args)
    {
        if (!HasLevel(uid, SharedSkillsSystem.StrengthId, SkillScaling.Expert) || args.Damage.GetTotal() <= 0
            || Transform(args.Target).Anchored || HasComp<MedievalUnthrowableComponent>(args.Target))
            return;
        var ev = new MeleeThrowOnHitStartEvent(args.Weapon, uid, Setting(SharedSkillsSystem.StrengthId, "KnockbackDistance"));
        RaiseLocalEvent(args.Target, ref ev);
        var direction = _transform.GetWorldPosition(args.Target) - _transform.GetWorldPosition(uid);
        if (ev.Distance <= 0 || direction.LengthSquared() < 0.001f)
            return;
        _throwing.TryThrow(args.Target, direction.Normalized() * ev.Distance, 2f, uid, recoil: false, playSound: false, doSpin: false);
    }
}
