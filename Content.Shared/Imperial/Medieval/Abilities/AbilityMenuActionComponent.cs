using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Abilities;

/// <summary>An action listed in the ability menu and added to the hotbar only by the player.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AbilityMenuActionComponent : Component;
