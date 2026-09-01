using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.BadSmell;

/// <summary>
/// Component that makes an entity smell worse when manipulated, stepped-on or received when thrown upon.
/// </summary>
[RegisterComponent]
public sealed partial class StinkyComponent: Component
{
    [DataField]
    public float StinkOnPickup = 2f;
    [DataField]
    public float StinkOnWalkOver = 1f;
    [DataField]
    public float StinkOnThrowReceived = 5f;
}
