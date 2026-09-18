using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Medieval.Stamina;

[RegisterComponent]
public sealed partial class StaminaStunArmorComponent : Component
{
    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public float FlatReduction = 0f;
}

public sealed class StaminaStunArmorQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; }

    public float Multiplier = 1f;
    public float FlatReduction = 0f;

    public StaminaStunArmorQueryEvent(SlotFlags slots = SlotFlags.All)
    {
        TargetSlots = slots;
    }
}
