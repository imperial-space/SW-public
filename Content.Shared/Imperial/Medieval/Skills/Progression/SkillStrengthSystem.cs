using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Grab;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Skills;

[ByRefEvent]
public record struct GetWieldHandRequirementEvent(EntityUid Item, int Hands, bool SuppressDamageBonus = false);
[ByRefEvent]
public record struct AdditionalSlotFitEvent(EntityUid Item, SlotFlags Slot, bool Allowed = false);
[ByRefEvent]
public record struct ForceActionDelayEvent(float Seconds);
[ByRefEvent]
public record struct PhysicalStrikeEvent(EntityUid Target, EntityUid Weapon, DamageSpecifier Damage, bool Thrown = false, bool Empowered = false);
[ByRefEvent]
public readonly record struct PhysicalStrikeLandedEvent(EntityUid Target, bool Empowered);

[RegisterComponent, NetworkedComponent]
public sealed partial class SkillBeltBackpackComponent : Component;

public sealed class SkillStrengthSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private float Setting(string key) => _prototypes.Index<SkillPrototype>(SharedSkillsSystem.StrengthId).Modifiers[key];
    private static int Level(SkillsComponent skills) => SkillScaling.Level(skills, SharedSkillsSystem.StrengthId);

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, GetWieldHandRequirementEvent>(OnWield);
        SubscribeLocalEvent<SkillsComponent, AdditionalSlotFitEvent>(OnSlot);
        SubscribeLocalEvent<SkillsComponent, StartGrabAttemptEvent>(OnGrab);
        SubscribeLocalEvent<SkillsComponent, StartPullAttemptEvent>(OnPull);
        SubscribeLocalEvent<SkillsComponent, ForceActionDelayEvent>(OnForceDelay);
        SubscribeLocalEvent<SkillsComponent, PhysicalStrikeEvent>(OnStrike);
        SubscribeLocalEvent<SkillsComponent, PhysicalStrikeLandedEvent>(OnLanded);
    }

    private void OnWield(EntityUid uid, SkillsComponent skills, ref GetWieldHandRequirementEvent args)
    {
        if (HasComp<GunComponent>(args.Item) || HasComp<MultiHandedItemComponent>(args.Item))
            return;
        if (Level(skills) >= SkillScaling.Master && HasComp<MeleeWeaponComponent>(args.Item))
            args.Hands = 0;
        else if (Level(skills) >= SkillScaling.Expert && HasComp<ToolComponent>(args.Item))
        {
            args.Hands = 0;
            args.SuppressDamageBonus = true;
        }
    }

    private void OnSlot(EntityUid uid, SkillsComponent skills, ref AdditionalSlotFitEvent args) =>
        args.Allowed |= Level(skills) >= SkillScaling.Master && args.Slot == SlotFlags.BELT && HasComp<SkillBeltBackpackComponent>(args.Item);

    private void OnGrab(EntityUid uid, SkillsComponent skills, StartGrabAttemptEvent args)
    {
        if (Level(skills) < SkillScaling.Trained)
            args.Cancel();
    }

    private void OnPull(EntityUid uid, SkillsComponent skills, StartPullAttemptEvent args)
    {
        if (Level(skills) < SkillScaling.Trained && (HasComp<MobStateComponent>(args.Pulled) || !HasComp<ItemComponent>(args.Pulled) || HasComp<SkillHeavyCargoComponent>(args.Pulled)))
            args.Cancel();
    }

    private void OnForceDelay(EntityUid uid, SkillsComponent skills, ref ForceActionDelayEvent args)
    {
        args.Seconds /= SkillScaling.Multiplier(Level(skills), Setting("ForceSpeedPerLevel"));
        if (Level(skills) >= SkillScaling.Legendary)
            args.Seconds = Math.Min(args.Seconds, Setting("PersonPickupDelay"));
    }

    private void OnStrike(EntityUid uid, SkillsComponent skills, ref PhysicalStrikeEvent args)
    {
        if (Level(skills) < SkillScaling.Legendary || args.Target == uid)
            return;
        var original = args.Damage;
        if (!new[] { "Blunt", "Slash", "Piercing" }.Any(type => original.DamageDict.TryGetValue(type, out var value) && value > 0))
            return;
        var multiplier = 1f;
        if (args.Thrown && TryComp<ItemComponent>(args.Weapon, out var item) &&
            (item.Size == "Large" || item.Size == "Huge" || item.Size == "Ginormous"))
            multiplier *= Setting("LargeThrowMultiplier");
        var state = EnsureComp<SkillProgressionComponent>(uid);
        if (_timing.CurTime >= state.NextHeavyHit)
        {
            multiplier *= 1f + Setting("HeavyHitBonus");
            args.Empowered = true;
        }
        var damage = args.Damage * 1f;
        // This capstone enhances physical attacks, never poison, fire or spell damage.
        foreach (var type in new[] { "Blunt", "Slash", "Piercing" })
            if (damage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                damage.DamageDict[type] = amount * multiplier;
        args.Damage = damage;
    }

    private void OnLanded(EntityUid uid, SkillsComponent skills, ref PhysicalStrikeLandedEvent args)
    {
        if (!args.Empowered || Level(skills) < SkillScaling.Legendary)
            return;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        state.NextHeavyHit = _timing.CurTime + TimeSpan.FromSeconds(Setting("HeavyHitCooldown"));
        Dirty(uid, state);
    }
}
