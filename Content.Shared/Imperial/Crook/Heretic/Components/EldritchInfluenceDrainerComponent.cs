using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EldritchInfluenceDrainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Time = 8f;

    [DataField, AutoNetworkedField]
    public bool Hidden;
}
