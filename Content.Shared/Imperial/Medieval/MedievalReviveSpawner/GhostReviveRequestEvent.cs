using Robust.Shared.Serialization;

/// <summary>
/// Imperial Medieval Revive
/// Запрос клиента серверу на возрождение
/// </summary>
[Serializable, NetSerializable]
public sealed class GhostReviveRequestEvent : EntityEventArgs;
