using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;

/// <summary>
/// This is used for...
/// </summary>
[Serializable, NetSerializable]
public enum SailVisuals : byte
{
    Folded
}

[Serializable, NetSerializable]
public enum SailVisualLayers : byte
{
    Unfolded,
    Folded
}

[RegisterComponent, NetworkedComponent, Serializable]
public sealed partial class SailComponent : Component
{
    [DataField("SailSize")]
    public int SailSize = 1;

    [DataField("Folded")]
    public bool Folded;

    [DataField("Push")]
    public bool Push = true;
}
