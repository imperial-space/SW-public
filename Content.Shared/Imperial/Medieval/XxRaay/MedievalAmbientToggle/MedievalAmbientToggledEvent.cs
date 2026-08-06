using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.XxRaay.MedievalAmbientToggle;

/// <summary>

/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalAmbientToggledEvent : EntityEventArgs
{
    public bool Enabled { get; }

    public MedievalAmbientToggledEvent(bool enabled)
    {
        Enabled = enabled;
    }
}
