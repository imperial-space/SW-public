using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SkillCheck;

[Serializable, NetSerializable]
public sealed class SkillCheckRequestEvent(ProtoId<SkillPrototype>? skill) : EntityEventArgs
{
    public readonly ProtoId<SkillPrototype>? Skill = skill;
}
