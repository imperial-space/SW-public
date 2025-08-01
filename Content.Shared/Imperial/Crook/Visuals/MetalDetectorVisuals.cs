using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security
{
    [Serializable, NetSerializable]
    public enum MetalDetectorVisualState
    {
        Off,
        Powered,
        Scanning,
        Alert
    }

    [Serializable, NetSerializable]
    public enum MetalDetectorVisuals
    {
        State
    }
}
