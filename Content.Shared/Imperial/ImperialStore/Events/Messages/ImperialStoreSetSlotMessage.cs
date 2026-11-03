using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ImperialStore;


/// <summary>
/// imperial medieval - asks to put a school into an essence slot, a null school empties the slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialStoreSetSlotMessage(int slot, string? school) : BoundUserInterfaceMessage
{
    public int Slot = slot;

    public string? School = school;
}
