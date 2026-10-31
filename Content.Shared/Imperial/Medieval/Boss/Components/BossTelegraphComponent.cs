using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossTelegraphComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";
    [AutoNetworkedField]
    public int HalfSize = 5;                                                    // Arena half-size in tiles
    [ViewVariables, AutoNetworkedField]
    public EntityUid Boss;
    [ViewVariables, AutoNetworkedField]
    public EntityUid Grid;                                                       // grid used for all mechanic tile positions
    [ViewVariables, AutoNetworkedField]
    public BossTelegraphPattern Pattern;
    [ViewVariables, AutoNetworkedField]
    public int Facing;                                                          // Rotation in quarter turns
    [ViewVariables, AutoNetworkedField]
    public Vector2i PatternOrigin;                                              // Mechanic center tile
    [ViewVariables, AutoNetworkedField]
    public List<Vector2i> MovementPath = new();
    [ViewVariables, AutoNetworkedField]
    public Vector2i? MovementDestination;
    [ViewVariables, AutoNetworkedField]
    public List<Vector2i> Danger = new();                                       // Damage tiles
    [ViewVariables, AutoNetworkedField]
    public List<Vector2i> Safe = new();                                         // Safe tiles
    [ViewVariables, AutoNetworkedField]
    public List<Vector2i> SpreadCenters = new();                                 // player tiles used for spread impacts
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> SpreadTargets = new();
    [ViewVariables, AutoNetworkedField]
    public Vector2i? StackCenter;                                               // Shared damage center tile
    [ViewVariables, AutoNetworkedField]
    public EntityUid? StackTarget;
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan AnimationStartTime;                                          // shared animation clock for the clients
    [ViewVariables, AutoNetworkedField]
    public bool SecondPhase;
    [ViewVariables, AutoNetworkedField]
    public bool ShowSecondColor;
    [ViewVariables, AutoNetworkedField]
    public bool Preload;
    [ViewVariables, AutoNetworkedField]
    public bool StoredStrike;
    [ViewVariables, AutoNetworkedField]
    public bool Impact;
    [ViewVariables, AutoNetworkedField]
    public bool SuppressDanger;
    [ViewVariables, AutoNetworkedField]
    public bool MovementCleave;
    [ViewVariables, AutoNetworkedField]
    public EntityUid? TankTarget;
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;                                                  // Warning starts
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;                                                    // Warning ends
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan SwitchTime;
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan RevealTime;
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan DangerRevealTime;
    public bool ShowDanger(TimeSpan now) => !SuppressDanger && (Preload ? now >= StartTime && now < EndTime : !(StoredStrike || BossTilePatterns.IsBlackout(Pattern) || BossTilePatterns.IsConstellation(Pattern)) || now >= DangerRevealTime && now < EndTime);
}
