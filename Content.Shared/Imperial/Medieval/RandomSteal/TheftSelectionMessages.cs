using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.RandomSteal.Events;

[Serializable, NetSerializable]
public enum TheftUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RequestTheftChoicesMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SelectTheftItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class TheftChoicesMessage(List<TheftItemChoice> items) : BoundUserInterfaceMessage
{
    public readonly List<TheftItemChoice> Items = items;
}

[Serializable, NetSerializable]
public sealed record TheftItemChoice(NetEntity Entity, string Name, string? Prototype);
