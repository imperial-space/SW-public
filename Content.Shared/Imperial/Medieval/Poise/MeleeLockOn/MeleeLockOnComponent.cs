using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeLockOnComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool TargetingMode;

    // Input ownership only; ranged spells keep their own targets and casting state.
    [ViewVariables, AutoNetworkedField]
    public bool RangedTargetingActive;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;
}
