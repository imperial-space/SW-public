using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public enum ForgedAssemblyVisuals : byte
{
    /// <summary>
    /// Ключ для передачи данных о деталях (руки, ноги, голова).
    /// </summary>
    head,
    right_arm,
    left_arm,
    right_hand,
    left_hand,
    right_leg,
    left_leg,
    core,
    torso
}
