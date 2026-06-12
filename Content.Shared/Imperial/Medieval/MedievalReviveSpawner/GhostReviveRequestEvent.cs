using Robust.Shared.Serialization;

/// <summary>
/// Imperial Medieval Revive
/// Запрос клиента серверу на возрождение
/// </summary>
namespace Content.Shared.Imperial.Medieval.MedievalReviveSpawner;

[Serializable, NetSerializable]
public sealed class GhostReviveRequestEvent : EntityEventArgs;
