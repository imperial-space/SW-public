using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;
using Content.Shared.Mind;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Content.Server.KillTracking;
using Content.Server.Pinpointer;
using Robust.Server.Player;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

public sealed class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRuleSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
    }

    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = true;

        // Спавним 10 машин искупления в случайных местах на станции
        SpawnRedemptionMachines();

        // Конвертируем всех текущих игроков при запуске правила
        ConvertAllCurrentPlayers(component);
    }

    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!AnyRuleActive())
            return;

        var activeRule = GetActiveRuleEntity();
        if (activeRule == null || !TryComp<SyndieBattleRuleComponent>(activeRule, out var component))
            return;

        MakeTraitor(ev.Mob, component);
    }

    private void MakeTraitor(EntityUid player, SyndieBattleRuleComponent? component = null)
    {

        if (!_mindSystem.TryGetMind(player, out var mindId, out var mind))
            return;

        if (component == null)
        {
            var activeRule = GetActiveRuleEntity();
            if (activeRule == null || !TryComp(activeRule, out component))
                return;
        }

        _roleSystem.MindAddRole(mindId, "MindRoleTraitor", mind);

        _traitorRuleSystem.MakeTraitor(player, new TraitorRuleComponent());

        AssignTraitorObjectives(player);

        if (!TryComp<ActorComponent>(player, out var actor))
            return;

    var scoreComp = EnsureComp<SyndieBattleScoreComponent>(player);
    scoreComp.PlayerId = actor.PlayerSession.UserId;
    }

    private void AssignTraitorObjectives(EntityUid player)
    {
        if (!_mindSystem.TryGetMind(player, out var mindId, out var mind))
            return;

        _mindSystem.TryAddObjective(mindId, mind, "SyndieBattleSurviveObjective");

        var maxDifficulty = 7f;
        var picked = 0;
        while (picked < 5 && maxDifficulty > 0f)
        {
            var objective = _objectivesSystem.GetRandomObjective(mindId, mind, "TraitorObjectiveGroups", maxDifficulty);
            if (objective is null)
                break;

            mind.Objectives.Add(objective.Value);
            var diff = Comp<ObjectiveComponent>(objective.Value).Difficulty;
            maxDifficulty -= diff;
            picked++;
        }
    }

    private void OnKillReported(ref KillReportedEvent ev)
    {
        if (!AnyRuleActive())
            return;

        if (ev.Primary is KillPlayerSource playerSource)
        {
            if (_playerManager.TryGetSessionById(playerSource.PlayerId, out var killerSession) &&
                killerSession.AttachedEntity != null &&
                TryComp<SyndieBattleScoreComponent>(killerSession.AttachedEntity.Value, out var killerScore))
            {
                killerScore.Score++;
            }
        }

        if (!TryComp<SyndieBattleScoreComponent>(ev.Entity, out var victimScore))
            return;

        var killerName = GetKillerName(ev.Primary);
        var victimName = GetEntityName(ev.Entity);
        var location = GetDeathLocation(ev.Entity);

        var message = Loc.GetString("syndiebattle-kill-detail",
            ("killer", killerName),
            ("victim", victimName),
            ("location", location));

        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, ev.Entity, false, false);
    }

    private bool AnyRuleActive()
    {
        var query = QueryAllRules();
        while (query.MoveNext(out _, out var comp, out _))
        {
            if (comp.Active)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Получает сущность активного правила SyndieBattle
    /// </summary>
    private EntityUid? GetActiveRuleEntity()
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.Active)
                return uid;
        }
        return null;
    }

    /// <summary>
    /// Конвертирует всех текущих игроков в предателей
    /// </summary>
    private void ConvertAllCurrentPlayers(SyndieBattleRuleComponent component)
    {
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out _, out _, out var mobState))
        {
            // Пропускаем мертвых игроков
            if (_mobStateSystem.IsDead(uid, mobState))
                continue;

            // Делаем игрока предателем
            MakeTraitor(uid, component);
        }
    }

    /// <summary>
    /// Спавнит 10 машин искупления в случайных местах на станции
    /// </summary>
    private void SpawnRedemptionMachines()
    {
        var activeRule = GetActiveRuleEntity();
        if (activeRule == null || !TryComp<SyndieBattleRuleComponent>(activeRule.Value, out var ruleComp))
            return;

        for (var i = 0; i < ruleComp.RedemptionMachineCount; i++)
        {
            if (!TryFindRandomTile(out _, out _, out _, out var coords))
                continue;

            Spawn("SyndieBattleRedemptionMachine", coords);
        }
    }

    /// <summary>
    /// Получает имя убийцы из KillSource
    /// </summary>
    private string GetKillerName(KillSource source)
    {
        switch (source)
        {
            case KillPlayerSource player:
                if (!_playerManager.TryGetSessionById(player.PlayerId, out var session))
                    return "Неизвестный игрок";
                if (session.AttachedEntity == null)
                    return "Неизвестный игрок";
                return MetaData(session.AttachedEntity.Value).EntityName;

            case KillNpcSource npc:
                if (Deleted(npc.NpcEnt))
                    return "Неизвестный NPC";
                return MetaData(npc.NpcEnt).EntityName;

            case KillEnvironmentSource:
                return "Окружение";
        }

        return "Неизвестно";
    }

    /// <summary>
    /// Получает имя сущности
    /// </summary>
    private string GetEntityName(EntityUid entity)
    {
    return MetaData(entity).EntityName;
    }

    /// <summary>
    /// Получает место смерти
    /// </summary>
    private string GetDeathLocation(EntityUid entity)
    {
        try
        {
            var location = _navMap.GetNearestBeaconString(entity);
            return string.IsNullOrEmpty(location) ? "неизвестном месте" : location;
        }
        catch
        {
            return "неизвестном месте";
        }
    }
}


