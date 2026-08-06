namespace Content.Shared.Imperial.Medieval.XxRaay.MedievalAmbientToggle;

/// <summary>

/// </summary>
public static class MedievalAmbientRules
{
    public static readonly HashSet<string> RuleIds = new()
    {
        "NearLegion",
        "NearLegionTown",
        "NearInsurgency",
        "NearInsurgencyTown",
        "NearVillage",
        "NearMyrmex",
        "NearMine",
        "NearSwamp",
        "NearSands",
        "NearTribe",
        "NearGoblin",
        "NearDark",
        "NearMage",
        "NearHell",
    };

    public static bool IsMedievalRule(string rulesId) => RuleIds.Contains(rulesId);
}
