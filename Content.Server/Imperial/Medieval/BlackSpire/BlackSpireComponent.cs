using System.Threading;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.BlackSpire;

[RegisterComponent]
public sealed partial class BlackSpireComponent : Component
{
    [DataField]
    public List<TimeSpan> StageDurations = new()
    {
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(10),
    };

    [DataField]
    public int Stage = 1;

    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan CurseDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public float NeedsMultiplier = 1.3f;

    [DataField]
    public float SkeletonChance = 0.4f;

    [DataField]
    public EntProtoId WeakSkeleton = "MedievalMobSkeletWeak";

    [DataField]
    public float CharacterSpawnMinRadius = 2f;

    [DataField]
    public float CharacterSpawnMaxRadius = 4f;

    [DataField]
    public DamageSpecifier RadiationDamage = new()
    {
        DamageDict = new() { ["Radiation"] = FixedPoint2.New(5) },
    };

    [DataField]
    public TimeSpan AggressionInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public float AggressionRadius = 15f;

    [DataField]
    public TimeSpan DamageAggressionDuration = TimeSpan.FromMinutes(2);

    [DataField]
    public bool Aggressive;

    [DataField]
    public float SpawnMinRadius = 1.5f;

    [DataField]
    public float SpawnMaxRadius = 6f;

    [DataField]
    public Dictionary<EntProtoId, float> SkeletonPool = new()
    {
        ["MedievalMobSkeletHalberdSpell"] = 1f,
        ["MedievalMobSkeletDaggerSpell"] = 1f,
        ["MedievalMobSkeletLegionSpell"] = 1f,
        ["MedievalMobSkeletSpearSpell"] = 1f,
        ["MedievalMobSkeletFighterSpell"] = 1f,
        ["MedievalMobSkeletMeatSpell"] = 10f,
        ["MedievalMobSkeletWeakSpell"] = 20f,
    };

    [DataField]
    public int InvincibilityEndedSpawnCount = 5;

    [DataField]
    public int SpawnPositionAttempts = 20;

    [DataField]
    public float SpawnClearance = 0.45f;

    [DataField]
    public TimeSpan SpawnDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId SpawnEffect = "MedievalLateMobSpawnEffect";

    [DataField]
    public SoundSpecifier SpawnSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/mob_spawn_effect.ogg");

    public CancellationTokenSource? DamageAggressionTimer;
    public bool DamageAggressionActive;
}
