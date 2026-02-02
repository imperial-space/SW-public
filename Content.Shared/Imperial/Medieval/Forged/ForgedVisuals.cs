using Robust.Shared.Serialization;

namespace Content.Shared.Forged;

[Serializable, NetSerializable]
public enum ForgedVisuals : byte
{
    /// <summary>
    /// Ключ для передачи данных о деталях (руки, ноги, голова).
    /// </summary>
    head,
    l_arm,
    r_arm,
    legs,
    core
}
