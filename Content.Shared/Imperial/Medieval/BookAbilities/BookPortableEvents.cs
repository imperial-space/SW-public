using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>Unpacking belongs to the portable item and does not require its maker's knowledge.</summary>
[Serializable, NetSerializable]
public sealed partial class BookUnpackDoAfterEvent : SimpleDoAfterEvent;
