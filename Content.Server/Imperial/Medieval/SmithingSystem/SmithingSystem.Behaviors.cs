using System.Linq;
using Content.Server.Damage.Components;
using Content.Shared.Armor;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee;
using SixLabors.ImageSharp.Formats;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem
{
    private void InitializeBehaviors()
    {
        SubscribeLocalEvent<UpgradeArmorOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeArmor);
        SubscribeLocalEvent<UpgradeWeaponOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(UpgradeWeapon);
        SubscribeLocalEvent<DeleteOnLowScoreOnSmithCompleteComponent, SmithingApplyBehaviorsEvent>(DeleteOnLowScore);
    }

    private void UpgradeArmor(Entity<UpgradeArmorOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        if (!TryComp<ArmorComponent>(args.Item, out var armorComponent))
        {
            return;
        }

        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        foreach (var key in armorComponent.Modifiers.FlatReduction.Keys)
        {
            armorComponent.Modifiers.FlatReduction[key] *= modifier.Modifier;
        }

        foreach (var key in armorComponent.Modifiers.Coefficients.Keys)
        {
            armorComponent.Modifiers.Coefficients[key] /= modifier.Modifier;
        }

        SetName(args.Item, modifier.Quality);
    }

    private void UpgradeWeapon(Entity<UpgradeWeaponOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        var modifier = GetBestModifier(args.Score, ent.Comp.ItemQualityTable);

        if (TryComp<MedievalMeleeResourceComponent>(args.Item, out var resourceComponent))
        {
            resourceComponent.FullModifier *= modifier.Modifier;
            resourceComponent.AlmostFullModifier *= modifier.Modifier;
            resourceComponent.DamagedModifier *= modifier.Modifier;
            resourceComponent.BadlyDamagedModifier *= modifier.Modifier;
            resourceComponent.BrokenModifier *= modifier.Modifier;
            resourceComponent.UpModifier *= modifier.Modifier;
        }

        if (TryComp<DamageOtherOnHitComponent>(args.Item, out var damageOtherOnHitComponent))
        {
            damageOtherOnHitComponent.Damage *= modifier.Modifier;
        }

        if (TryComp<MeleeWeaponComponent>(args.Item, out var meleeWeaponComponent))
        {
            meleeWeaponComponent.Damage *= modifier.Modifier;
        }

        SetName(args.Item, modifier.Quality);
        Dirty(ent);
    }

    private void DeleteOnLowScore(Entity<DeleteOnLowScoreOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        if (args.Score < ent.Comp.Threshold)
        {
            QueueDel(args.Item);
        }
    }

    private void SetName(EntityUid entityUid, ItemQuality quality)
    {
        var name = Identity.Name(entityUid, EntityManager);
        var append = _itemQualityDecorators[quality];

        var newName = append + name + append;
        _metaDataSystem.SetEntityName(entityUid, newName);
    }

    private Dictionary<ItemQuality, string> _itemQualityDecorators = new()
    {
        { ItemQuality.Worst, "---"},
        { ItemQuality.ReallyBad, "--"},
        { ItemQuality.Bad, "-"},
        { ItemQuality.Default, "+"},
        { ItemQuality.Good, "++"},
        { ItemQuality.Excellent, "+++"},

    };

    private SmithQualityModifiers GetBestModifier(int score, Dictionary<int, SmithQualityModifiers> table)
    {
        var bestData = table.MinBy(x => x.Key).Value; // Самый плохой по умолчанию

        foreach (var (threshold, data) in table)
        {
            if (score > threshold && data.Modifier > bestData.Modifier)
            {
                bestData = data;
            }
        }

        return bestData;
    }
}
