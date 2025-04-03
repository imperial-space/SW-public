using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.ICCVar;

public sealed partial class ICCVars
{
    public static readonly CVarDef<bool> QueueEnabled =
        CVarDef.Create("medieval.queue_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool>
            WaveShaderEnabled = CVarDef.Create("medieval.wave_shader_enabled", false, CVar.CLIENT | CVar.ARCHIVE); // RETURN TO TRUE AFTER FIX OPTIONS

    public static readonly CVarDef<bool> EnableLanguageFonts =
        CVarDef.Create("lang.enable_fonts", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
