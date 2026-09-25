using System.Linq;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string VitalityId = "Vitality";

    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, HealingModifyEvent>(OnGetDamageModifiers);
        SubscribeLocalEvent<SkillsComponent, DamageModifyEvent>(OnPoisonDamage);
    }

    private void OnPoisonDamage(EntityUid uid, SkillsComponent comp, DamageModifyEvent args)
    {
        if (SkillScaling.Level(comp, VitalityId) < SkillScaling.Legendary
            || !args.Damage.DamageDict.TryGetValue("Poison", out var poison) || poison <= 0)
            return;
        args.Damage *= 1f;
        args.Damage.DamageDict.Remove("Poison");
    }

    private void OnGetDamageModifiers(EntityUid uid, SkillsComponent comp, ref HealingModifyEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);
        var multiplier = SkillScaling.HealingReceived(level,
            proto.Modifiers["LowHealingReceivedPerLevel"], proto.Modifiers["HealingReceivedPerLevel"]);
        args.Damage *= 1f; // Never modify a cached reagent/item damage specification.
        foreach (var (type, amount) in args.Damage.DamageDict.ToArray())
        {
            if (amount < 0)
                args.Damage.DamageDict[type] = amount * multiplier;
        }
    }
}
