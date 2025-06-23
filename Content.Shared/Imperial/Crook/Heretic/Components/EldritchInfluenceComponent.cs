using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EldritchInfluenceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Spent = false;
}
