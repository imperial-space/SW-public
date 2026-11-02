using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Inventory;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed partial class MedievalArmorIntegritySystem
{
    public static Color GetIntegrityColor(float currentArmorHp, float maxArmorHp)
    {
        if (currentArmorHp <= 0f || maxArmorHp <= 0f)
            return Color.Red;

        return MathHelper.CloseTo(currentArmorHp, maxArmorHp)
            ? Color.Lime
            : Color.Gray;
    }

    private void OnArmorExamined(Entity<MedievalArmorIntegrityComponent> ent, ref ExaminedEvent args)
    {
        if (!_net.IsServer)
            return;

        using (args.PushGroup(nameof(MedievalArmorIntegrityComponent)))
        {
            args.PushMarkup(Loc.GetString("armor-integrity-examine-current",
                ("current", ent.Comp.CurrentArmorHP.ToString("0.##")),
                ("max", ent.Comp.MaxArmorHP.ToString("0.##")),
                ("color", GetIntegrityColor(ent.Comp.CurrentArmorHP, ent.Comp.MaxArmorHP).ToHexNoAlpha())));
            args.PushMarkup(Loc.GetString("armor-integrity-examine-maximum",
                ("maximum", MathF.Round(ent.Comp.ContainerArmorHP, 2))));
        }
    }

    private void OnCharacterExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        var intelligence = HasComp<GhostComponent>(args.Examiner)
            ? 20
            : _skills.GetSkillLevel(args.Examiner, SharedSkillsSystem.IntelligenceId);
        if (intelligence <= 8)
            return;

        var equippedArmor = GetEquippedArmor(ent.Comp, includeBroken: true);
        if (equippedArmor.Count == 0)
            return;

        var currentArmorHp = 0f;
        var maxArmorHp = 0f;

        foreach (var armor in equippedArmor)
        {
            currentArmorHp += armor.Comp.CurrentArmorHP;
            maxArmorHp += armor.Comp.MaxArmorHP;
        }

        var percentage = maxArmorHp <= 0f
            ? 0f
            : Math.Clamp(currentArmorHp / maxArmorHp * 100f, 0f, 100f);

        if (intelligence >= 20)
        {
            args.PushMarkup(Loc.GetString("armor-integrity-exact",
                ("percentage", (int)MathF.Round(percentage))));
            return;
        }

        args.PushMarkup(Loc.GetString(GetArmorIntegrityStatus(percentage)));
    }

    private static string GetArmorIntegrityStatus(float percentage)
    {
        return percentage switch
        {
            <= 0 => "armor-integrity-broken",
            <= 25 => "armor-integrity-almost-broken",
            <= 50 => "armor-integrity-heavy-damaged",
            <= 75 => "armor-integrity-damaged",
            < 100 => "armor-integrity-scratched",
            _ => "armor-integrity-full",
        };
    }
}
