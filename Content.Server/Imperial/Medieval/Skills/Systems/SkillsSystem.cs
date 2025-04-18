using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAgility();
        InitializeVitality();
        InitializeIntelligence();

        SubscribeLocalEvent<SkillsComponent, SkillLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnLevelChanged(EntityUid uid, SkillsComponent comp, ref SkillLevelChangedEvent args)
    {
        if (args.Id == VitalityId)
            VitalityLevelSet(uid, args.Level, args.OldLevel);
        if (args.Id == IntelligenceId)
            IntelligenceLevelSet(uid, args.Level, args.OldLevel);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_proto.TryIndex<JobPrototype>(args.JobId ?? string.Empty, out var protoJob) ||
            !protoJob.ApplySkills)
            return;

        SetSkills(args.Mob, args.Profile.Skills);
    }

    public void SetSkills(EntityUid uid, Dictionary<string, int> skills)
    {
        var comp = EnsureComp<SkillsComponent>(uid);

        foreach (var skill in skills)
        {
            if (!_proto.TryIndex<SkillPrototype>(skill.Key, out var skillProto))
                continue;

            var oldLevel = comp.Levels.GetValueOrDefault(skill.Key, 10);

            comp.Levels[skillProto.ID] = skill.Value;
            var ev = new SkillLevelChangedEvent(skill.Key, skill.Value, oldLevel);
            RaiseLocalEvent(uid, ref ev);
        }

        Dirty(uid, comp);
    }
}
