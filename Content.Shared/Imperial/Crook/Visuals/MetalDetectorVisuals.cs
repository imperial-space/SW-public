using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security
{
    [Serializable, NetSerializable]
    public enum MetalDetectorVisualState
    {
        Off,
        Powered,
        Scanning,
        Warning,
        Alert
    }

    [Serializable, NetSerializable]
    public enum MetalDetectorVisuals
    {
        State
    }
}
