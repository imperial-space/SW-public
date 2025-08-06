using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class RequestPlagueMenuDataMessage : EntityEventArgs
{
    public readonly NetEntity Ent;

    public RequestPlagueMenuDataMessage(NetEntity ent)
    {
        Ent = ent;
    }
}
