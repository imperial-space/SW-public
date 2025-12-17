using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Skills;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FightForLifeActionComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan? NextUseTime;

    [AutoNetworkedField]
    public EntityUid? ActionEntity;
}
