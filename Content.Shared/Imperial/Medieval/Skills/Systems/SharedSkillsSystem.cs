using Content.Shared.Popups;
using System.Linq;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public const int Points = 3;

    public override void Initialize()
    {
        base.Initialize();
        InitializeCombat();
        InitializeAgility();
        InitializeEndurance();
        InitializeIntelligence();
        InitializeVitality();

        InitializeDesc();

        SubscribeLocalEvent<SkillsComponent, MapInitEvent>(OnSkillsMapInit);
        SubscribeLocalEvent<SkillsComponent, AfterAutoHandleStateEvent>(OnSkillsState);
    }

    protected (SkillPrototype, int) GetSkill(EntityUid uid, string id)
    {
        var proto = _proto.Index<SkillPrototype>(id);

        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return (proto, 10);

        return (proto, skillComponent.Levels.TryGetValue(id, out var val) ? val : 10);
    }

    public static int GetPointsCost(int level)
    {
        if (level == 10)
            return 0;
        var sum = 0;

        if (level > 10)
        {
            for (var i = 0; i < level; i++)
            {
                sum += i switch
                {
                    <= 9 => 0,
                    >= 19 => -4,
                    >= 17 => -3,
                    >= 14 => -2,
                    >= 10 => -1,
                };
            }
        }
        else
        {
            for (var i = 9; i >= level; i--)
            {
                sum += (i - level) switch
                {
                    <= 0 => 1,
                    <= 4 => 2,
                    <= 7 => 2,
                    >= 8 => 3,
                };
            }
        }

        return sum;
    }

    public static Dictionary<string, int> GetDefaultSkillLevels(IPrototypeManager prototypes)
    {
        return prototypes.EnumeratePrototypes<SkillPrototype>()
            .ToDictionary(skill => skill.ID, _ => 10);
    }

    public static int GetRemainingPoints(IPrototypeManager prototypes, IReadOnlyDictionary<string, int> levels)
    {
        var points = Points;
        foreach (var skill in prototypes.EnumeratePrototypes<SkillPrototype>())
            points += GetPointsCost(levels.GetValueOrDefault(skill.ID, 10));

        return points;
    }

    public static bool TryValidateSkillLevels(
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, int> levels,
        out Dictionary<string, int> validated,
        int minimumLevel = 1,
        int maximumLevel = 20)
    {
        validated = new();
        var skills = prototypes.EnumeratePrototypes<SkillPrototype>().ToList();
        if (levels.Count != skills.Count)
            return false;

        foreach (var skill in skills)
        {
            if (!levels.TryGetValue(skill.ID, out var level) || level < minimumLevel || level > maximumLevel)
                return false;

            validated[skill.ID] = level;
        }

        return GetRemainingPoints(prototypes, validated) >= 0;
    }

    public int GetSkillLevel(EntityUid uid, string skill)
    {
        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return 1;
        return skillComponent.Levels.GetValueOrDefault(skill, 1);
    }

    private void OnSkillsMapInit(EntityUid uid, SkillsComponent component, MapInitEvent args)
    {
        var changed = new SkillProfileChangedEvent(uid);
        RaiseLocalEvent(ref changed);
    }

    private void OnSkillsState(EntityUid uid, SkillsComponent component, ref AfterAutoHandleStateEvent args)
    {
        var changed = new SkillProfileChangedEvent(uid);
        RaiseLocalEvent(ref changed);
    }

    /// <summary>Changes one known attribute and notifies its independent effect systems.</summary>
    public void SetSkillLevel(EntityUid uid, string skill, int level)
    {
        if (!_proto.HasIndex<SkillPrototype>(skill) || !TryComp<SkillsComponent>(uid, out var skills))
            return;

        var previous = SkillScaling.Level(skills, skill);
        level = Math.Clamp(level, 1, SkillScaling.Legendary);
        if (level == previous)
            return;

        skills.Levels[skill] = level;
        var changed = new SkillLevelChangedEvent(skill, level, previous);
        RaiseLocalEvent(uid, ref changed);
        Dirty(uid, skills);

        var profile = new SkillProfileChangedEvent(uid);
        RaiseLocalEvent(ref profile);
    }

    public bool HasSkill(EntityUid uid, string skill)
    {
        return TryComp<SkillsComponent>(uid, out var skillComponent)
            && skillComponent.Levels.ContainsKey(skill);
    }
}
