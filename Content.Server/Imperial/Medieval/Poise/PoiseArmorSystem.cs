using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;

namespace Content.Server.Imperial.Medieval.Poise;

public sealed class PoiseArmorSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly PoiseSystem _poiseSystem = default!;

    private readonly Dictionary<EntityUid, EntityUid> _armorWearers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoiseArmorComponent, GotEquippedEvent>(OnArmorEquipped);
        SubscribeLocalEvent<PoiseArmorComponent, GotUnequippedEvent>(OnArmorUnequipped);
        SubscribeLocalEvent<PoiseArmorComponent, MedievalArmorBrokenStateChangedEvent>(OnArmorBrokenStateChanged);
        SubscribeLocalEvent<PoiseComponent, MapInitEvent>(OnPoiseMapInit);
        SubscribeLocalEvent<PoiseArmorComponent, ComponentShutdown>(OnArmorShutdown);

    }

    private void OnPoiseMapInit(EntityUid uid, PoiseComponent component, MapInitEvent args)
    {
        RecalculatePoiseArmor(uid);
    }

    private void OnArmorEquipped(EntityUid uid, PoiseArmorComponent component, GotEquippedEvent args)
    {
        _armorWearers[uid] = args.Equipee;

        RecalculatePoiseArmor(args.Equipee);
    }

    private void OnArmorUnequipped(EntityUid uid, PoiseArmorComponent component, GotUnequippedEvent args)
    {
        _armorWearers.Remove(uid);

        RecalculatePoiseArmor(args.Equipee);
    }

    private void OnArmorBrokenStateChanged(EntityUid uid, PoiseArmorComponent component, ref MedievalArmorBrokenStateChangedEvent args)
    {
        if (!_armorWearers.TryGetValue(uid, out var wearer))
            return;

        RecalculatePoiseArmor(wearer);
    }

    private void OnArmorShutdown(EntityUid uid, PoiseArmorComponent component, ComponentShutdown args)
    {
        if (!_armorWearers.TryGetValue(uid, out var wearer))
            return;

        _armorWearers.Remove(uid);

        if (Exists(wearer))
            RecalculatePoiseArmor(wearer);
    }

    private void RecalculatePoiseArmor(EntityUid wearer)
    {
        if (!TryComp<PoiseComponent>(wearer, out var poise))
            return;

        // NPC poise capacity and protection are configured on the mob itself.
        if (poise.Npc)
            return;

        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        var weightedSum = 0f;
        var totalWeight = 0f;
        var totalPoiseIncrease = 0f;
        var hasPoiseArmor = false;

        var slotEnumerator = _inventorySystem.GetSlotEnumerator((wearer, inventory));

        while (slotEnumerator.NextItem(out var item, out var slot))
        {
            if (!TryComp<PoiseArmorComponent>(item, out var armor))
                continue;

            _armorWearers[item] = wearer;

            // resistance-only accessories do not count as armor.
            if (!armor.ProvidesArmorBonuses)
                continue;

            hasPoiseArmor = true;

            var isPrimaryArmor = (slot.SlotFlags & SlotFlags.OUTERCLOTHING) != 0;

            var broken = TryComp<MedievalArmorIntegrityComponent>(item, out var integrity) && integrity.IsBroken;
            var multiplier = PoiseArmorPresets.ResolveEffectiveMultiplier(armor, broken);
            // share capacity resolution with the recovery bonus check.
            var poiseIncrease = armor.GetPoiseIncrease();

            PoiseArmorPresets.AddMultiplierContribution(multiplier, isPrimaryArmor, ref weightedSum, ref totalWeight);

            totalPoiseIncrease += poiseIncrease;
        }

        var armorPoiseIncrease = (int)MathF.Round(totalPoiseIncrease, MidpointRounding.AwayFromZero);
        var combinedMultiplier = PoiseArmorPresets.CalculateCombinedMultiplier(weightedSum, totalWeight);
        var changed = MathF.Abs(poise.PoiseMultiplier - combinedMultiplier) > 0.0001f || poise.HasPoiseArmor != hasPoiseArmor || poise.ArmorPoiseIncrease != armorPoiseIncrease;

        poise.PoiseMultiplier = combinedMultiplier;
        poise.HasPoiseArmor = hasPoiseArmor;
        poise.ArmorPoiseIncrease = armorPoiseIncrease;

        if (!poise.PoiseBroken && poise.PoiseDamage >= PoiseSystem.GetEffectivePoiseThreshold(poise))
        {
            _poiseSystem.BreakPoise(wearer, poise);
            return;
        }

        if (changed)
            Dirty(wearer, poise);
    }

    private static class PoiseArmorPresets
    {
        public const float LightMultiplier = 1.25f;
        public const float MediumMultiplier = 1.50f;
        public const float HeavyMultiplier = 1.80f;

        public static float GetMultiplierWeight(float multiplier, bool isPrimaryArmor)
        {
            if (isPrimaryArmor)
                return 4f;

            if (MathF.Abs(multiplier - 1f) < 0.0001f)
                return 0.5f;

            return 1f;
        }

        public static void AddMultiplierContribution(float multiplier, bool isPrimaryArmor, ref float weightedSum, ref float totalWeight)
        {
            var weight = GetMultiplierWeight(multiplier, isPrimaryArmor);

            weightedSum += multiplier * weight;
            totalWeight += weight;
        }

        public static float CalculateCombinedMultiplier(float weightedSum, float totalWeight)
        {
            if (totalWeight <= 0f)
                return 1f;

            var multiplier = weightedSum / totalWeight;

            return RoundMultiplier(multiplier);
        }

        public static float RoundMultiplier(float multiplier)
        {
            const float step = 0.05f;
            return MathF.Round(multiplier / step, MidpointRounding.AwayFromZero) * step;
        }

        public static float ResolveEffectiveMultiplier(PoiseArmorComponent component, bool broken)
        {
            var multiplier = ResolveMultiplier(component);

            if (broken)
                multiplier = GetBrokenMultiplier(multiplier);

            return multiplier;
        }

        public static float GetMultiplier(PoiseArmorClass armorClass)
        {
            return armorClass switch
            {
                PoiseArmorClass.Light => LightMultiplier,
                PoiseArmorClass.Medium => MediumMultiplier,
                PoiseArmorClass.Heavy => HeavyMultiplier,
                _ => 1f,
            };
        }

        public static float GetBrokenMultiplier(float multiplier)
        {
            if (multiplier <= 1f)
                return multiplier * 0.5f;

            return 1f + (multiplier - 1f) * 0.5f;
        }
        public static float ResolveMultiplier(PoiseArmorComponent component)
        {
            return component.MultiplierOverride
                ?? GetMultiplier(component.ArmorClass);
        }

    }

}
