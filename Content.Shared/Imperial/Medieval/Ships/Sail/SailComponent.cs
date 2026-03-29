using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, Serializable]
public sealed partial class SailComponent : Component
{
    [DataField("SailSize")]
    public int SailSize = 1;

    [DataField("Folded")]
    public bool Folded;

    [DataField("Helm")]
    public bool Helm;

    [DataField("Push")]
    public bool Push = true;
}
