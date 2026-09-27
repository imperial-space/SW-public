using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly IBanManager _ban = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
        InitializeStrength();
        InitializeAgility();
        InitializeVitality();
        InitializeIntelligence();

        SubscribeLocalEvent<SkillsComponent, SkillLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeNetworkEvent<SetSkillLevelMessage>(OnSetSkillLevel);
    }
    public bool TryGetSkill(EntityUid uid, string skillId, out int level)
    {
        level = 0;
        if (!TryComp<SkillsComponent>(uid, out var skills))
            return false;

        return skills.Levels.TryGetValue(skillId, out level);
    }

    private void OnLevelChanged(EntityUid uid, SkillsComponent comp, ref SkillLevelChangedEvent args)
    {
        switch (args.Id)
        {
            case VitalityId:
                VitalityLevelSet(uid, args.Level, args.OldLevel);
                break;
            case IntelligenceId:
                IntelligenceLevelSet(uid, args.Level, args.OldLevel);
                break;
            case AgilityId:
                AgilityLevelSet(uid, args.Level, args.OldLevel);
                break;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_proto.TryIndex<JobPrototype>(args.JobId ?? string.Empty, out var protoJob) ||
            !protoJob.ApplySkills)
            return;

        // Existing saved profiles may predate the point-cost rebalance. Never ban for migration.
        var valid = TryValidateSkillLevels(_proto, args.Profile.Skills, out var validated);
        if (!valid)
            _popup.PopupEntity(Loc.GetString("skills-profile-migrated"), args.Mob, args.Mob);
        var levels = valid
            ? validated
            : GetDefaultSkillLevels(_proto);
        EntityManager.System<Content.Server.Imperial.Medieval.Skills.Progression.SkillMagicSystem>().RegisterProfession(args.Mob, args.JobId);
        SetSkills(args.Mob, levels);
        EntityManager.System<Content.Server.Imperial.Medieval.Skills.Progression.SkillMagicSystem>().GrantStartingGrimoire(args.Mob);
    }

    private void OnSetSkillLevel(SetSkillLevelMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
        {
            _ban.CreateServerBan(args.SenderSession.UserId, args.SenderSession.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, Loc.GetString("skills-autoban-set"));
            return;
        }
        var uid = GetEntity(msg.Target);
        var comp = EnsureComp<SkillsComponent>(uid);

        var dict = new Dictionary<string, int>(comp.Levels);
        dict[msg.Skill] = msg.Level;
        SetSkills(uid, dict);
    }

    public void SetSkills(EntityUid uid, Dictionary<string, int> skills)
    {
        var comp = EnsureComp<SkillsComponent>(uid);
        EnsureComp<SkillProgressionComponent>(uid);
        var requested = new Dictionary<string, int>(skills);
        var previous = new Dictionary<string, int>(comp.Levels);

        // Publish the whole new profile before handlers inspect other attributes.
        foreach (var skill in _proto.EnumeratePrototypes<SkillPrototype>())
            comp.Levels[skill.ID] = Math.Clamp(requested.GetValueOrDefault(skill.ID, 10), 1, 20);

        foreach (var skill in _proto.EnumeratePrototypes<SkillPrototype>())
        {
            var oldLevel = previous.GetValueOrDefault(skill.ID, 10);
            var ev = new SkillLevelChangedEvent(skill.ID, comp.Levels[skill.ID], oldLevel);
            RaiseLocalEvent(uid, ref ev);
        }

        Dirty(uid, comp);
        var changed = new SkillProfileChangedEvent(uid);
        RaiseLocalEvent(ref changed);
    }

    public void ApplySkills(EntityUid uid, Dictionary<string, int> skills)
    {
        var magic = EntityManager.System<Content.Server.Imperial.Medieval.Skills.Progression.SkillMagicSystem>();
        magic.RegisterProfession(uid, null);
        SetSkills(uid, skills);
        magic.GrantStartingGrimoire(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1f);

        UpdateAgility(frameTime);
    }

}
