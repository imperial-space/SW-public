using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.RemoteStore;

[Serializable, NetSerializable]
public enum RemoteStoreUI : byte
{
    Key,
}

[Serializable, NetSerializable]
public struct RemoteStoreStateEntry(EntityUid uid, SpriteSpecifier.Rsi icon, string name)
{
    public EntityUid Uid = uid;
    public SpriteSpecifier.Rsi Icon = icon;
    public string Name = name;
}

[Serializable, NetSerializable]
public sealed class ChangeStoreMessage(EntityUid store) : BoundUserInterfaceMessage
{
    public EntityUid Store = store;
}

[Serializable, NetSerializable]
public sealed class RemoteStoreUIState(List<RemoteStoreStateEntry> stores) : BoundUserInterfaceState
{
    public List<RemoteStoreStateEntry> Stores = stores;
}
