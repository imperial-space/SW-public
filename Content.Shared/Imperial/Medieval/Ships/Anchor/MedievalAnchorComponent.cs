using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public enum MedievalAnchorVisuals : byte
{
    Enabled
}

[Serializable, NetSerializable]
public enum MedievalAnchorVisualLayers : byte
{
    Base
}

[RegisterComponent]
public sealed partial class MedievalAnchorComponent : Component
{
    [DataField("Enabled")]
    public bool Enabled;

    [DataField("upState")]
    public string UpState = "up";

    [DataField("downState")]
    public string DownState = "down";
}
