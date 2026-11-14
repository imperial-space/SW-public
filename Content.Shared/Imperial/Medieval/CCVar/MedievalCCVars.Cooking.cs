using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.CCVar;

public sealed partial class MedievalCCVars
{
    public static readonly CVarDef<float> PotTakeRateLimitPeriod =
        CVarDef.Create("medieval.pot_take_rate_limit_period", 2f, CVar.SERVER);

    public static readonly CVarDef<int> PotTakeRateLimitCount =
        CVarDef.Create("medieval.pot_take_rate_limit_count", 6, CVar.SERVER);
}
