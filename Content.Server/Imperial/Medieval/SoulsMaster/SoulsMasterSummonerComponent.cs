/// <summary>
/// Spellward International add;
/// Periodically summons goons around its summoner while the summoner is alive
/// </summary>
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Medieval.SoulsMaster;

[RegisterComponent]
public sealed partial class SoulsMasterSummonerComponent : Component
{
    [DataField]
    public EntProtoId SummonPrototype = "MedievalMobSkeletWeakSpell";

    [DataField]
    public int SummonCount = 1;

    [DataField]
    public float SummonRadius = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSummon;

    [DataField]
    public TimeSpan InitialDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);
}
