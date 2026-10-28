using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossActorMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<BossTelegraphStylePrototype> Style = "DefaultBossTelegraph";
    [DataField, AutoNetworkedField]
    public BossActorMarkerKind Kind;
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;
    [ViewVariables, AutoNetworkedField]
    public EntityUid Boss;
    [ViewVariables, AutoNetworkedField]
    public EntityUid Target;
}
