using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>A skill requirement for the ordinary construction menu and its server-side recipes.</summary>
[DataDefinition]
public sealed partial class SkillConstructionCondition : IConstructionAvailabilityCondition
{
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Skill;

    [DataField(required: true)]
    public int Level;

    public bool IsAvailable(EntityUid user)
    {
        return IoCManager.Resolve<IEntityManager>().TryGetComponent<SkillsComponent>(user, out var skills)
            && SkillScaling.Level(skills, Skill.Id) >= Level;
    }

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction) => IsAvailable(user);

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        var skill = IoCManager.Resolve<IPrototypeManager>().Index(Skill);
        return new ConstructionGuideEntry
        {
            Localization = "skills-construction-requirement",
            Arguments = new (string, object)[] { ("skill", Loc.GetString(skill.Name)), ("level", Level) },
        };
    }
}
