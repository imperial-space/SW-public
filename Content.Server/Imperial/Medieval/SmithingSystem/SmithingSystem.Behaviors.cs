using Content.Shared.Armor;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.MedievalMeleeResource.Components;

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

        var modifier = ent.Comp.GetBestModifier(args.Score);

        foreach (var key in armorComponent.Modifiers.FlatReduction.Keys)
        {
            armorComponent.Modifiers.FlatReduction[key] *= modifier.Modifier;
        }

        foreach (var key in armorComponent.Modifiers.Coefficients.Keys)
        {
            armorComponent.Modifiers.Coefficients[key] /= modifier.Modifier;
        }

        SetName(ent, modifier.Quality);
    }

    private void UpgradeWeapon(Entity<UpgradeWeaponOnSmithCompleteComponent> ent, ref SmithingApplyBehaviorsEvent args)
    {
        if (!TryComp<MedievalMeleeResourceComponent>(args.Item, out var resourceComponent))
        {
            return;
        }

        var modifier = ent.Comp.GetBestModifier(args.Score);

        resourceComponent.FullModifier *= modifier.Modifier;
        resourceComponent.AlmostFullModifier *= modifier.Modifier;
        resourceComponent.DamagedModifier *= modifier.Modifier;
        resourceComponent.BadlyDamagedModifier *= modifier.Modifier;
        resourceComponent.BrokenModifier *= modifier.Modifier;
        resourceComponent.UpModifier *= modifier.Modifier;

        SetName(args.Item, modifier.Quality);
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
        var append = _appendByItemQuality[quality];

        var newName = append + name + append;
        _metaDataSystem.SetEntityName(entityUid, newName);
    }

    private Dictionary<ItemQuality, string> _appendByItemQuality = new()
    {
        { ItemQuality.Bad, "-"},
        { ItemQuality.Default, string.Empty},
        { ItemQuality.Good, "+"},
        { ItemQuality.Excellent, "++"},

    };
}
