using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public sealed partial class SkillPickCraftEvent : SimpleDoAfterEvent;
