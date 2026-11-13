using Content.Shared.Damage;
using Content.Shared.Inventory;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed partial class MedievalArmorIntegritySystem
{
    private static float GetArmorDamage(float damage)
    {
        foreach (var (threshold, armorDamage) in MedievalArmorIntegrityComponent.DamageCurve)
        {
            if (damage <= threshold)
                return armorDamage;
        }

        return damage * MedievalArmorIntegrityComponent.HighDamageMultiplier;
    }

    private static float GetArmorRelevantDamage(DamageSpecifier damage, DamageableComponent damageable)
    {
        var totalDamage = 0f;
        foreach (var (damageType, amount) in damage.DamageDict)
        {
            if (amount <= 0f || damageType == "Structural" || damageType == "Bloodloss" || damageType == "Asphyxiation" || !damageable.Damage.DamageDict.ContainsKey(damageType))
            {
                continue;
            }
            totalDamage += amount.Float();
        }

        return totalDamage;
    }

    private static float GetArmorSpreadWeight(SlotFlags slot)
    {
        if ((slot & SlotFlags.OUTERCLOTHING) != 0)
            return MedievalArmorIntegrityComponent.OuterWeight;

        if ((slot & SlotFlags.HEAD) != 0)
            return MedievalArmorIntegrityComponent.HeadWeight;

        if ((slot & SlotFlags.LEGS) != 0)
            return MedievalArmorIntegrityComponent.LegsWeight;

        if ((slot & SlotFlags.GLOVES) != 0)
            return MedievalArmorIntegrityComponent.GlovesWeight;

        if ((slot & SlotFlags.FEET) != 0)
            return MedievalArmorIntegrityComponent.FeetWeight;

        return MedievalArmorIntegrityComponent.DefaultWeight;
    }

    private void OnDamagePreModify(Entity<DamageableComponent> ent, ref DamagePreModifyEvent args)
    {
        if (!_net.IsServer || args.IgnoreArmorDamage || !args.Damage.AnyPositive() || !TryComp<InventoryComponent>(ent, out var inventory))
        {
            return;
        }

        var incomingDamage = GetArmorRelevantDamage(args.Damage, ent.Comp);
        var armorDamage = GetArmorDamage(incomingDamage);

        DamageEquippedArmorByAmount(ent.Owner, inventory, armorDamage);
    }

    private void DamageEquippedArmorByAmount(EntityUid wearer, InventoryComponent inventory, float armorDamage)
    {
        if (armorDamage <= 0f)
            return;

        var equippedArmor = GetEquippedArmor(inventory, includeBroken: false);
        if (equippedArmor.Count == 0)
            return;

        if (armorDamage <= MedievalArmorIntegrityComponent.FocusDamageThreshold)
        {
            var weakestArmor = GetWeakestArmor(equippedArmor);
            DamageArmor(weakestArmor, armorDamage, wearer);
            return;
        }

        SpreadArmorDamage(wearer, inventory, armorDamage);
    }

    private void SpreadArmorDamage(EntityUid wearer, InventoryComponent inventory, float armorDamage)
    {
        var weightedArmor = new List<(Entity<MedievalArmorIntegrityComponent> Armor, float Weight)>();
        var totalWeight = 0f;

        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);

        while (enumerator.NextItem(out var item, out var slotDef))
        {
            if (!TryComp<MedievalArmorIntegrityComponent>(item, out var armor) || armor.IsBroken)
                continue;

            var weight = GetArmorSpreadWeight(slotDef.SlotFlags);

            weightedArmor.Add(((item, armor), weight));
            totalWeight += weight;
        }

        if (weightedArmor.Count == 0 || totalWeight <= 0f)
            return;

        foreach (var (armor, weight) in weightedArmor)
        {
            var damage = armorDamage * (weight / totalWeight);
            DamageArmor(armor, damage, wearer);
        }
    }

    public void DamageArmor(Entity<MedievalArmorIntegrityComponent> ent, float damage, EntityUid? effectTarget = null)
    {
        if (!_net.IsServer)
            return;

        if (ent.Comp.IsBroken || damage <= 0f)
            return;

        SetCurrentArmorHP(ent, ent.Comp.CurrentArmorHP - damage, effectTarget);
    }

    public bool HasUnbrokenArmor(InventoryComponent inventory)
    {
        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);

        while (enumerator.NextItem(out var item))
        {
            if (TryComp<MedievalArmorIntegrityComponent>(item, out var armorIntegrity) && !armorIntegrity.IsBroken)
                return true;
        }

        return false;
    }

    private List<Entity<MedievalArmorIntegrityComponent>> GetEquippedArmor(
        InventoryComponent inventory,
        bool includeBroken)
    {
        var equippedArmor = new List<Entity<MedievalArmorIntegrityComponent>>();
        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);

        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<MedievalArmorIntegrityComponent>(item, out var armorIntegrity) ||
                !includeBroken && armorIntegrity.IsBroken)
            {
                continue;
            }

            equippedArmor.Add((item, armorIntegrity));
        }

        return equippedArmor;
    }

    private static Entity<MedievalArmorIntegrityComponent> GetWeakestArmor(List<Entity<MedievalArmorIntegrityComponent>> equippedArmor)
    {
        var lowestHpArmor = equippedArmor[0];

        foreach (var armor in equippedArmor)
        {
            if (armor.Comp.CurrentArmorHP < lowestHpArmor.Comp.CurrentArmorHP)
                lowestHpArmor = armor;
        }

        var weakestArmor = lowestHpArmor;
        var lowestHpPercent = GetArmorDurabilityPercent(lowestHpArmor.Comp);

        foreach (var armor in equippedArmor)
        {
            if (armor.Owner == lowestHpArmor.Owner)
                continue;

            var armorPercent = GetArmorDurabilityPercent(armor.Comp);

            if (armorPercent > lowestHpPercent - MedievalArmorIntegrityComponent.WeakPercentMargin)
                continue;

            if (armor.Comp.CurrentArmorHP > lowestHpArmor.Comp.CurrentArmorHP * MedievalArmorIntegrityComponent.WeakHpMultiplier)
                continue;

            if (weakestArmor.Owner == lowestHpArmor.Owner || armor.Comp.CurrentArmorHP < weakestArmor.Comp.CurrentArmorHP)
            {
                weakestArmor = armor;
            }
        }

        return weakestArmor;
    }

    private static float GetArmorDurabilityPercent(MedievalArmorIntegrityComponent armor)
    {
        if (armor.MaxArmorHP <= 0f)
            return 0f;

        return armor.CurrentArmorHP / armor.MaxArmorHP;
    }
}
