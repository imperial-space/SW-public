using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BossTraversalComponent : Component
{
    public BossTraversalKind Kind;
    public BossTraversalPhase Phase;
    public EntityUid Arena;
    public EntityUid Grid;
    public DamageSpecifier Damage = new();
    public DamageSpecifier SpreadDamage = new();
    public DamageSpecifier FlankDamage = new();
    public BossTelegraphPattern Pattern;
    public int Direction;
    public int Distance;
    public int Position;
    public int RemainingRain;
    public float RainWarningTime;
    public List<int> RainSizes = new();
    public int RainWave;
    public int RainResolved;
    public int RainConcurrent;
    public float RainSpawnInterval;
    public float RainDoubleChance;
    public int RainMaxConcurrent;
    public float RainMinSpawnInterval;
    public float RainRampSeconds;
    public int RainActiveCap;
    public float RainCurrentInterval;
    [AutoPausedField]
    public TimeSpan RainPressureStart;
    public List<EntityUid> RainDrops = new();
    public List<EntityUid> RainVisuals = new();
    public float MotionFrom;
    public float MotionTo;
    public bool Flank;
    public BossTelegraphPattern? FlankPattern;
    public bool Completed;
    public Vector2i Origin;
    public List<EntityUid> Targets = new();
    [AutoPausedField]
    public TimeSpan Deadline;
    [AutoPausedField]
    public TimeSpan DashStart;
}
