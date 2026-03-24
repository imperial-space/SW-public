namespace Content.Shared.Imperial.Medieval.XxRaay.MedievalAmbientToggle;

/// <summary>
/// Rule IDs from medieval_ambient_if.yml used to select medieval ambient music.
/// </summary>
public static class MedievalAmbientRules
{
    public static readonly HashSet<string> RuleIds = new(StringComparer.Ordinal)
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
