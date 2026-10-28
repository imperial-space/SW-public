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
    [AutoNetworkedField] public Vector2 Origin;
    [AutoNetworkedField] public Vector2 Direction = Vector2.UnitX;
    [AutoNetworkedField] public float Length;
    [AutoNetworkedField] public float Width = BossExecutionLineVisuals.Width;
    [AutoNetworkedField] public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";
    [AutoNetworkedField] public bool Resolved;
    [AutoNetworkedField, AutoPausedField] public TimeSpan StartTime;
    [AutoNetworkedField, AutoPausedField] public TimeSpan ImpactTime;
    [AutoNetworkedField, AutoPausedField] public TimeSpan EndTime;
}
