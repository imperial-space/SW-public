using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> ParryStaminaDamage =
        CVarDef.Create("parry.stamina_damage", 20f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> ParryDesyncTolerance =
        CVarDef.Create("parry.desync_tolerance", 0.1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> ParryUseDelay =
        CVarDef.Create("parry.use_delay", 1.25f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
