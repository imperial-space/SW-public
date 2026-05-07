using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public enum MedievalAnchorVisuals : byte
{
    Enabled
}

[RegisterComponent]
public sealed partial class MedievalAnchorComponent : Component
{
    [DataField("Enabled")]
    public bool Enabled;
}
