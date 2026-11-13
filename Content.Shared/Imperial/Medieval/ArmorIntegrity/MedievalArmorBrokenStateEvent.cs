using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[ByRefEvent]
public readonly record struct MedievalArmorBrokenStateChangedEvent(bool IsBroken);