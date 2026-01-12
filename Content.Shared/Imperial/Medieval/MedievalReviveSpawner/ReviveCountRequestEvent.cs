using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.MedievalReviveSpawner;

/// <summary>
/// Событие, отправляемое клиенту с количеством воскрешений.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReviveCountRequestEvent : EntityEventArgs;

