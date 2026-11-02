using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Prediction;

/// <summary>
/// Reports the authoritative server tick the client has processed before its
/// current predicted input. This is separate from the client's predicted tick.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalSetLastRealTickEvent : EntityEventArgs
{
    public readonly GameTick Tick;

    public MedievalSetLastRealTickEvent(GameTick tick)
    {
        Tick = tick;
    }
}
