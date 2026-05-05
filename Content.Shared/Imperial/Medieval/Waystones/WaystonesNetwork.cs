using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Waystones;

[Serializable, NetSerializable]
public enum WaystoneUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class WaystoneUpdateState : BoundUserInterfaceState
{
    public readonly List<WaystoneInfo> Waystones;
    public WaystoneUpdateState(List<WaystoneInfo> waystones) => Waystones = waystones;
}

[Serializable, NetSerializable]
public struct WaystoneInfo
{
    public NetEntity Entity;
    public string Name;
    public int PriceOut;
    public int PriceIn;

    public WaystoneInfo(NetEntity entity, string name, int priceIn, int priceOut)
    {
        Entity = entity;
        Name = name;
        PriceOut = priceOut;
        PriceIn = priceIn;
    }
}

[Serializable, NetSerializable]
public sealed class WaystoneSelectMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity TargetWaystone;
    public WaystoneSelectMessage(NetEntity targetWaystone) => TargetWaystone = targetWaystone;
}

[Serializable, NetSerializable]
public sealed partial class WaystoneTeleportDoAfterEvent : SimpleDoAfterEvent { }
