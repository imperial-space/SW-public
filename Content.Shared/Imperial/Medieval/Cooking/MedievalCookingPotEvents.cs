using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - a player clicked an ingredient shown above a pot and wants it back in hand.
///     Everything in it comes from the client, so the server checks it all again.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedievalPotTakeRequestEvent(NetEntity pot, NetEntity item) : EntityEventArgs
{
    public NetEntity Pot = pot;
    public NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed partial class MedievalPotEmptyDoAfterEvent : SimpleDoAfterEvent;
