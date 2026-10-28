using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossEncounterStep
{
    [DataField]
    public BossTelegraphPattern Pattern;
    [DataField]
    public BossTelegraphPattern? Alternate;
    [DataField]
    public bool Combo;
    [DataField]
    public bool TankBuster;
    [DataField]
    public bool RandomCorner;
    [DataField]
    public List<BossTelegraphPattern> Queue = new();
    [DataField]
    public float CastTime = 3;
    [DataField]
    public float FollowupDelay = 4;
    [DataField]
    public bool SequentialConstellation;
    [DataField]
    public string Damage = "cleave";
    [DataField]
    public bool MixedSentence;
    [DataField]
    public bool StackFirst;
    [DataField]
    public bool CrossSentence;
    [DataField]
    public BossTelegraphPattern CrossSentenceTarget = BossTelegraphPattern.Spread;
    [DataField]
    public BossTelegraphPattern? SimultaneousPattern;
    [DataField]
    public int SpreadTargets = 4;
    [DataField]
    public BossTelegraphPattern? VigilFollowup;
    [DataField]
    public string? CastName;
    [DataField]
    public bool PreloadOnly;
    [DataField]
    public bool StoredStrike;
    [DataField]
    public bool HideCast;
    [DataField]
    public float? RecoveryAfter;
    [DataField]
    public BossTraversalKind? Traversal;
    [DataField]
    public bool ConeSpread;
    [DataField]
    public bool ConeFlank;
    [DataField]
    public int RainCount = 4;
    [DataField]
    public float RainWarningTime = 3;
    [DataField]
    public List<int> RainSizes = new()
    {
        3
    };
    [DataField]
    public float RainCastTime = 1.5f;
    [DataField]
    public int RainConcurrent = 8;
    [DataField]
    public float RainSpawnInterval = 0.5f;
    [DataField]
    public float RainDoubleChance;
    [DataField]
    public int RainMaxConcurrent = 8;
    [DataField]
    public float RainMinSpawnInterval = 0.5f;
    [DataField]
    public float RainRampSeconds = 60;
}
