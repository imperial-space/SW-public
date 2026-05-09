using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> ParryStaminaDamage =
        CVarDef.Create("parry.stamina_damage", 20f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
