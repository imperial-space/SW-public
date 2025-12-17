using System.Linq;
using Content.Server.Damage.Components;
using Content.Shared.Armor;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem
{
    private readonly Dictionary<ItemQuality, string> _itemQualityDecorators = new()
    {
        { ItemQuality.Worst, "отвр. " },
        { ItemQuality.ReallyBad, "ужс. " },
        { ItemQuality.Bad, "плох. " },
        { ItemQuality.Default, "хор. " },
        { ItemQuality.Good, "отл. " },
        { ItemQuality.Excellent, "шедевр. " },
    };

    private void InitializeBehaviors()
    {
        SubscribeLocalEvent<UpgradeArmorOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeArmor);
        SubscribeLocalEvent<UpgradeWeaponOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeWeapon);
        SubscribeLocalEvent<DeleteOnLowScoreOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(DeleteOnLowScore);
    }

    private void UpgradeArmor(Entity<UpgradeArmorOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        if (!TryComp<ArmorComponent>(args.Item, out var armorComponent))
            return;

        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        foreach (var key in armorComponent.Modifiers.FlatReduction.Keys.ToArray())
        {
            armorComponent.Modifiers.FlatReduction[key] =
                MathF.Round(armorComponent.Modifiers.FlatReduction[key] * modifier.Modifier, 2);
        }

        foreach (var key in armorComponent.Modifiers.Coefficients.Keys.ToArray())
        {
            armorComponent.Modifiers.Coefficients[key] =
                MathF.Round(armorComponent.Modifiers.Coefficients[key] / modifier.Modifier, 2);
        }

        EntityManager.DirtyEntity(args.Item);
        SetName(args.Item, modifier.Quality);
    }

    private void UpgradeWeapon(Entity<UpgradeWeaponOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        if (TryComp<MedievalMeleeResourceComponent>(args.Item, out var resourceComponent))
        {
            resourceComponent.FullModifier = MathF.Round(resourceComponent.FullModifier * modifier.Modifier, 2);
            resourceComponent.AlmostFullModifier =
                MathF.Round(resourceComponent.AlmostFullModifier * modifier.Modifier, 2);
            resourceComponent.DamagedModifier = MathF.Round(resourceComponent.DamagedModifier * modifier.Modifier, 2);
            resourceComponent.BadlyDamagedModifier =
                MathF.Round(resourceComponent.BadlyDamagedModifier * modifier.Modifier, 2);
            resourceComponent.BrokenModifier = MathF.Round(resourceComponent.BrokenModifier * modifier.Modifier, 2);
            resourceComponent.UpModifier = MathF.Round(resourceComponent.UpModifier * modifier.Modifier, 2);
        }

        if (TryComp<DamageOtherOnHitComponent>(args.Item, out var damageOtherOnHitComponent))
            damageOtherOnHitComponent.Damage *= modifier.Modifier;

        if (TryComp<MeleeWeaponComponent>(args.Item, out var meleeWeaponComponent))
            meleeWeaponComponent.Damage *= modifier.Modifier;

        EntityManager.DirtyEntity(args.Item);
        SetName(args.Item, modifier.Quality);
    }


    private void DeleteOnLowScore(Entity<DeleteOnLowScoreOnSmithCompleteComponent> ent,
        ref SmithingApplyBehaviorsEvent args)
    {
        if (args.Score < ent.Comp.Threshold)
            QueueDel(args.Item);
    }

    private void SetName(EntityUid entityUid, ItemQuality quality)
    {
        var name = Identity.Name(entityUid, EntityManager);
        var append = _itemQualityDecorators[quality];

        var newName = append + name;
        _metaDataSystem.SetEntityName(entityUid, newName);
    }

    private SmithQualityModifiers GetBestModifier(int score, Dictionary<int, SmithQualityModifiers> table)
    {
        var bestThreshold = int.MinValue;
        SmithQualityModifiers? best = null;

        foreach (var (threshold, data) in table)
        {
            if (score >= threshold && threshold >= bestThreshold)
            {
                bestThreshold = threshold;
                best = data;
            }
        }

        return best ?? table.MinBy(x => x.Key).Value;
    }
}
