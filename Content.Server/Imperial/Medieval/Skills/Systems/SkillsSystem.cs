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
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly IBanManager _ban = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    // Compatibility handlers live with their legacy skill implementation.
    // New modules subscribe to skill events from their own systems.
    private readonly Dictionary<string, Action<EntityUid, int, int>> _levelHandlers = new();
    private readonly List<Action<float>> _updateHandlers = new();

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

    private void RegisterLevelHandler(string id, Action<EntityUid, int, int> handler)
    {
        _levelHandlers.Add(id, handler);
    }

    private void RegisterUpdateHandler(Action<float> handler)
    {
        _updateHandlers.Add(handler);
    }

    private void OnLevelChanged(EntityUid uid, SkillsComponent comp, ref SkillLevelChangedEvent args)
    {
        if (_levelHandlers.TryGetValue(args.Id, out var handler))
            handler(uid, args.Level, args.OldLevel);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_proto.TryIndex<JobPrototype>(args.JobId ?? string.Empty, out var protoJob) ||
            !protoJob.ApplySkills)
            return;

        var sum = Points + 1;
        foreach (var skill in args.Profile.Skills)
        {
            sum += GetPointsCost(skill.Value);
        }
        if (sum < 0)
        {
            _ban.CreateServerBan(args.Player.UserId, args.Player.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, Loc.GetString("skills-autoban-points"));
            return;
        }

        ApplySkills(args.Mob, args.Profile.Skills, args.JobId);
    }

    private void OnSetSkillLevel(SetSkillLevelMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
        {
            _ban.CreateServerBan(args.SenderSession.UserId, args.SenderSession.Name, null, null, null, 0, Shared.Database.NoteSeverity.High, Loc.GetString("skills-autoban-set"));
            return;
        }
        var uid = GetEntity(msg.Target);
        EnsureComp<SkillsComponent>(uid);
        SetSkillLevel(uid, msg.Skill, msg.Level);
    }

    /// <summary>Publishes a complete profile before any effect observes another attribute.</summary>
    public void SetSkills(EntityUid uid, Dictionary<string, int> skills)
    {
        var comp = EnsureComp<SkillsComponent>(uid);
        var requested = new Dictionary<string, int>(skills);
        var previous = new Dictionary<string, int>(comp.Levels);

        foreach (var skill in _proto.EnumeratePrototypes<SkillPrototype>())
            comp.Levels[skill.ID] = Math.Clamp(requested.GetValueOrDefault(skill.ID, SkillScaling.Baseline), 1, SkillScaling.Legendary);

        foreach (var skill in _proto.EnumeratePrototypes<SkillPrototype>())
        {
            var changed = new SkillLevelChangedEvent(skill.ID, comp.Levels[skill.ID], previous.GetValueOrDefault(skill.ID, SkillScaling.Baseline));
            RaiseLocalEvent(uid, ref changed);
        }

        Dirty(uid, comp);
        var profile = new SkillProfileChangedEvent(uid);
        RaiseLocalEvent(ref profile);
    }

    /// <summary>Applies a character profile with optional profession context for independent modules.</summary>
    public void ApplySkills(EntityUid uid, Dictionary<string, int> skills, string? jobId = null)
    {
        EnsureComp<SkillsComponent>(uid);
        var applying = new SkillProfileApplyingEvent(jobId);
        RaiseLocalEvent(uid, ref applying);
        SetSkills(uid, skills);
        var applied = new SkillProfileAppliedEvent(jobId);
        RaiseLocalEvent(uid, ref applied);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1f);

        foreach (var handler in _updateHandlers)
            handler(frameTime);
    }

}
