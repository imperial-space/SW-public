using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>A workshop which can be dismantled and reassembled by a learned ability.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BookPackableWorkbenchComponent : Component;

/// <summary>
/// The visible, targetable part of a spike trap. The persistent controller is an invisible map marker.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SpikeTrapVisualComponent : Component;
