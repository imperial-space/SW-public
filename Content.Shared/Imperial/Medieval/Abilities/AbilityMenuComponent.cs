using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Abilities;

/// <summary>
/// Gives a body one ability menu. Independent ability sources share this component.
/// The menu action belongs to the body; each source owns the abilities it contributes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AbilityMenuComponent : Component
{
    public EntityUid? MenuAction;
}
