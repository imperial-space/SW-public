using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.MedievalReviveSpawner;

/// <summary>
/// Ответ от сервера клиенту: "вот сколько у тебя осталось возрождений".
/// </summary>
[Serializable, NetSerializable]
public sealed class ReviveCountResponseEvent : EntityEventArgs
{
    public int CurrentCount { get; set; }
    public int MaxCount { get; set; }

    public ReviveCountResponseEvent(int currentCount, int maxCount)
    {
        CurrentCount = currentCount;
        MaxCount = maxCount;
    }

    // Для INetSerializable
    public ReviveCountResponseEvent() { }
}
