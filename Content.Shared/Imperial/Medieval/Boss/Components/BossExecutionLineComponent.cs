using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossExecutionLineComponent : Component
{
    [AutoNetworkedField] public EntityUid Boss;
    [AutoNetworkedField] public EntityUid Target;
    [AutoNetworkedField] public EntityUid Grid;
    [AutoNetworkedField] public int HalfSize;
    [AutoNetworkedField] public Vector2 Origin;                                  // Line start on the arena grid
    [AutoNetworkedField] public Vector2 Direction = Vector2.UnitX;               // Line direction
    [AutoNetworkedField] public float Length;                                    // Line length in tiles
    [AutoNetworkedField] public float Width = BossExecutionLineVisuals.Width;    // Line width in tiles
    [AutoNetworkedField] public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";
    [AutoNetworkedField] public bool Resolved;
    [AutoNetworkedField, AutoPausedField] public TimeSpan StartTime;
    [AutoNetworkedField, AutoPausedField] public TimeSpan ImpactTime;            // Damage time
    [AutoNetworkedField, AutoPausedField] public TimeSpan EndTime;
}
