using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Cooking;

[Serializable, NetSerializable]
public sealed class MedievalPotTakeRequestEvent(NetEntity pot, NetEntity item) : EntityEventArgs
{
    public NetEntity Pot = pot;
    public NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed partial class MedievalPotEmptyDoAfterEvent : SimpleDoAfterEvent;
