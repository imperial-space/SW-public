using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CameraReset;

[Serializable, NetSerializable]
public sealed class ResetCameraOnGridEnterEvent(NetEntity entity) : EntityEventArgs
{
    public NetEntity Entity = entity;
}

[Serializable, NetSerializable]
public sealed class RequestResetCameraOnGridEnterEvent : EntityEventArgs
{
}
