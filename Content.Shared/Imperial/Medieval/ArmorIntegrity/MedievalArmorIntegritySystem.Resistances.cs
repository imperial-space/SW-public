using Content.Shared.Armor;
using Content.Shared.CCVar;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed partial class MedievalArmorIntegritySystem
{
    private void OnArmorExamine(Entity<MedievalArmorIntegrityComponent> ent, ref ArmorExamineEvent args)
    {
        var resistance = GetExplosionResistance(ent.Comp);
        // examine uses the same live global multiplier as actual protection.
        var value = MathF.Round((1f - GetScaledExplosionDamageCoefficient(resistance.DamageCoefficient)) * 100, 2);

        if (value == 0)
            return;

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString(resistance.Examine, ("value", value)));
    }

    private void OnGetExplosionResistance(
        Entity<MedievalArmorIntegrityComponent> ent,
        ref GetExplosionResistanceEvent args)
    {
        ApplyExplosionResistance(GetExplosionResistance(ent.Comp), ref args);
    }

    private void OnRelayedExplosionResistance(
        Entity<MedievalArmorIntegrityComponent> ent,
        ref InventoryRelayedEvent<GetExplosionResistanceEvent> args)
    {
        var resistance = GetExplosionResistance(ent.Comp);
        if (resistance.Worn)
            ApplyExplosionResistance(resistance, ref args.Args);
    }

    private void SetArmorResistances(
        Entity<MedievalArmorIntegrityComponent> ent,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        if (TryComp<ArmorComponent>(ent, out var armor))
            SetArmorResistances((ent.Owner, armor), resistances);
    }

    private void SetArmorResistances(
        Entity<ArmorComponent> ent,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        ent.Comp.Modifiers = CreateModifierSet(resistances);
        Dirty(ent);
    }

    private static void CopyArmorResistances(
        DamageModifierSet modifiers,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        foreach (var (damageType, coefficient) in modifiers.Coefficients)
        {
            resistances[damageType] = new MedievalArmorResistance
            {
                Coefficient = coefficient,
            };
        }

        foreach (var (damageType, flatReduction) in modifiers.FlatReduction)
        {
            if (!resistances.TryGetValue(damageType, out var resistance))
            {
                resistance = new MedievalArmorResistance();
                resistances[damageType] = resistance;
            }

            resistance.FlatReduction = flatReduction;
        }
    }

    private static void GenerateBrokenResistances(MedievalArmorIntegrityComponent armor)
    {
        armor.BrokenResistances.Clear();

        foreach (var (damageType, resistance) in armor.UnbrokenResistances)
        {
            armor.BrokenResistances[damageType] = new MedievalArmorResistance
            {
                Coefficient = 1f - MedievalArmorIntegrityComponent.BrokenDefenseRetention * (1f - resistance.Coefficient), FlatReduction = resistance.FlatReduction * MedievalArmorIntegrityComponent.BrokenDefenseRetention,
            };
        }
    }

    private MedievalArmorExplosionResistance CopyExplosionResistance(EntityUid entity)
    {
        var resistance = new MedievalArmorExplosionResistance();
        var baseResistance = GetExplosionDamageCoefficient(entity, string.Empty);
        resistance.DamageCoefficient = baseResistance;

        string? wornProbe = null;
        var wornProbeCoefficient = baseResistance;
        if (baseResistance != 1f)
            wornProbe = string.Empty;

        if (baseResistance != 0f)
        {
            foreach (var explosion in _prototype.EnumeratePrototypes<ExplosionPrototype>())
            {
                var coefficient = GetExplosionDamageCoefficient(entity, explosion.ID);
                var modifier = coefficient / baseResistance;
                if (modifier != 1f)
                    resistance.Modifiers[explosion.ID] = modifier;

                if (wornProbe == null && coefficient != 1f)
                {
                    wornProbe = explosion.ID;
                    wornProbeCoefficient = coefficient;
                }
            }
        }

        if (wornProbe != null)
            resistance.Worn = GetRelayedExplosionDamageCoefficient(entity, wornProbe) == wornProbeCoefficient;

        return resistance;
    }

    private float GetExplosionDamageCoefficient(EntityUid entity, string explosionPrototype)
    {
        var ev = new GetExplosionResistanceEvent(explosionPrototype);
        RaiseLocalEvent(entity, ref ev);
        return ev.DamageCoefficient;
    }

    private float GetRelayedExplosionDamageCoefficient(EntityUid entity, string explosionPrototype)
    {
        var ev = new InventoryRelayedEvent<GetExplosionResistanceEvent>(
            new GetExplosionResistanceEvent(explosionPrototype),
            entity);
        RaiseLocalEvent(entity, ev);
        return ev.Args.DamageCoefficient;
    }

    private static bool IsDefaultExplosionResistance(MedievalArmorExplosionResistance resistance)
    {
        return resistance.DamageCoefficient == 1f &&
               resistance.Modifiers.Count == 0;
    }

    private static MedievalArmorExplosionResistance GetExplosionResistance(
        MedievalArmorIntegrityComponent component)
    {
        return component.IsBroken
            ? component.BrokenExplosionResistance
            : component.UnbrokenExplosionResistance;
    }

    // apply the global multiplier once per armor contribution.
    private void ApplyExplosionResistance(
        MedievalArmorExplosionResistance resistance,
        ref GetExplosionResistanceEvent args)
    {
        var coefficient = resistance.DamageCoefficient;
        if (resistance.Modifiers.TryGetValue(args.ExplosionPrototype, out var modifier))
            coefficient *= modifier;

        args.DamageCoefficient *= GetScaledExplosionDamageCoefficient(coefficient);
    }

    private float GetScaledExplosionDamageCoefficient(float coefficient)
    {
        var multiplier = _configuration.GetCVar(CCVars.MedievalArmorExplosionResistanceMultiplier);
        return SharedExplosionSystem.ScaleArmorExplosionDamageCoefficient(coefficient, multiplier);
    }

    private static DamageModifierSet CreateModifierSet(
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        var modifiers = new DamageModifierSet();

        foreach (var (damageType, resistance) in resistances)
        {
            if (!MathHelper.CloseTo(resistance.Coefficient, 1f))
                modifiers.Coefficients[damageType] = resistance.Coefficient;

            if (!MathHelper.CloseTo(resistance.FlatReduction, 0f))
                modifiers.FlatReduction[damageType] = resistance.FlatReduction;
        }

        return modifiers;
    }
}
