using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SkillGrantedActionsComponent : Component
{
    [DataField] public EntityUid? RecoveryAction;
    [DataField] public int AppliedStrength = 10;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan RecoveryReadyAt;
}
