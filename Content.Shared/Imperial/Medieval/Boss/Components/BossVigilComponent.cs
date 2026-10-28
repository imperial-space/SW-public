using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossVigilComponent : Component
{
    [AutoNetworkedField] public EntityUid Arena;
    [AutoNetworkedField] public EntityUid Grid;
    [AutoNetworkedField] public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";
    [AutoNetworkedField] public Color OriginalAmbient;
    [AutoNetworkedField] public bool Resolved;
    [AutoNetworkedField] public List<EntityUid> Victims = new();
    [AutoNetworkedField, AutoPausedField] public TimeSpan StartTime;
    [AutoNetworkedField, AutoPausedField] public TimeSpan ResolveTime;
}
