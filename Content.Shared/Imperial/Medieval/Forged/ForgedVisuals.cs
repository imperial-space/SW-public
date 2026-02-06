using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public enum ForgedVisuals : byte
{
    /// <summary>
    /// Ключ для передачи данных о деталях (руки, ноги, голова).
    /// </summary>
    head,
    r_arm,
    l_arm,
    r_hand,
    l_hand,
    legs,
    core
}
