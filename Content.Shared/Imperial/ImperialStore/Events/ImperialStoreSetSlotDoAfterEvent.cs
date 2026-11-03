using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ImperialStore;


/// <summary>
/// imperial medieval - raised on the store when the change of an essence slot is finished or cancelled.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ImperialStoreSetSlotDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Slot;

    [DataField]
    public string School = string.Empty;

    /// <summary>
    /// The effect around the user, removed when the change ends.
    /// </summary>
    [DataField]
    public NetEntity Effect = NetEntity.Invalid;

    public override DoAfterEvent Clone() => this;
}
