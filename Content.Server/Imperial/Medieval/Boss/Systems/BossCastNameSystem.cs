using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossCastNameSystem : EntitySystem
{
    public string NameKey(EntityUid boss, string suffix)
        => (TryComp<BossTelegraphSettingsComponent>(boss, out var settings) ? settings.CaptionPrefix : "Boss_Encounter_") + suffix;

    public string CastName(EntityUid boss, BossTelegraphPattern pattern)
        => Robust.Shared.Localization.Loc.GetString(CastNameKey(boss, pattern));

    public string CastNameKey(EntityUid boss, BossTelegraphPattern pattern, bool sentencing = false)
    {
        if (TryComp<BossTelegraphSettingsComponent>(boss, out var settings) &&
            (sentencing ? settings.PreparedCastNames : settings.CastNames).TryGetValue(pattern, out var name)) return name;
        return NameKey(boss, (sentencing ? "Sentencing_" : "") + (pattern switch
        {
            BossTelegraphPattern.Sinistra => "Occidens",
            BossTelegraphPattern.Dextra => "Oriens",
            BossTelegraphPattern.Frons => "Septentrio",
            BossTelegraphPattern.Tergum => "Meridies",
            BossTelegraphPattern.Rejection => "Deaths_Bliss",
            BossTelegraphPattern.Invitation => "Deaths_Blight",
            BossTelegraphPattern.Cross => "Cross",
            BossTelegraphPattern.Divide => "Divide",
            BossTelegraphPattern.Spread => "Severance",
            BossTelegraphPattern.Stack => "Communion",
            BossTelegraphPattern.BlueFirst or BossTelegraphPattern.PurpleFirst => "Constellation",
            _ => "Vigil"
        }));
    }
}
