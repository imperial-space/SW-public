using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // Imperial medieval start
    /// <summary>Seconds to hold parryable melee consequences. Clamped to 0-100 ms by the parry system.</summary>
    public static readonly CVarDef<float> ParryConfirmationBuffer =
        CVarDef.Create("parry.confirmation_buffer", 0.05f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
    // Imperial medieval end

    public static readonly CVarDef<float> ParryDesyncTolerance =
        CVarDef.Create("parry.desync_tolerance", 0.1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<int> ParryReadySoundType =
        CVarDef.Create("parry.ready_sound_type", 0, CVar.CLIENT | CVar.ARCHIVE);
}

