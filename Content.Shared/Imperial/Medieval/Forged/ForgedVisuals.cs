using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public enum ForgedVisuals : byte
{
    /// <summary>
    /// Ключ для передачи данных о деталях (руки, ноги, голова).
    /// </summary>
    head,
    eyes,
    right_arm,
    left_arm,
    right_hand,
    left_hand,
    right_leg,
    left_leg,
    right_foot,
    left_foot,
    core,
    torso,
    upgrade1
}
