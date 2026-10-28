using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossBrazierAnimationComponent : Component
{
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;
    [DataField, AutoNetworkedField]
    public int PhaseOffsetFrames;
}
