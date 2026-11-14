using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.Boss;

[CVarDefs]
public sealed class BossHudCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("hud.boss_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> Scale =
        CVarDef.Create("hud.boss_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<bool> CustomPosition =
        CVarDef.Create("hud.boss_custom_position", false, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> PositionX =
        CVarDef.Create("hud.boss_position_x", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> PositionY =
        CVarDef.Create("hud.boss_position_y", 0.85f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
