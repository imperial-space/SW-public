using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.ICCVar;

public sealed partial class ICCVars
{
    public static readonly CVarDef<bool>
            WaveShaderEnabled = CVarDef.Create("medieval.wave_shader_enabled", true, CVar.CLIENT | CVar.ARCHIVE);

    public static readonly CVarDef<bool> EnableLanguageFonts =
        CVarDef.Create("lang.enable_fonts", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
