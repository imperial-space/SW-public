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

    // Number of entities spawned each time the summon cooldown completes
    [DataField]
    public int SummonCount = 1;
	
	// Cool effect proc'd when summon is spawned
	[DataField]
	public EntProtoId SummonEffect = "IceBarrierSpellCastEffectBeginner";

    // Maximum number of this summoner's living summons that may exist at once
    [DataField]
    public int MaxSummons = 3;

    [DataField]
    public float SummonRadius = 2f;

    // Number of random positions attempted for each summon before that spawn is skipped
    // Summons must be on a non-space floor tile that is not blocked by walls or mobs
    [DataField]
    public int SpawnAttempts = 5;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSummon;

    [DataField]
    public TimeSpan InitialDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    // Summons currently owned by this component. Dead or deleted entities stop the summoning
    public readonly HashSet<EntityUid> ActiveSummons = new();

    // Whether the summoner had an active combat target during the previous update
    public bool WasAggroed;
}
