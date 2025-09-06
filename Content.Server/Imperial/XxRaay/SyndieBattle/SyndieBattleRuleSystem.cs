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

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

public sealed class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRuleSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = true;

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

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!AnyRuleActive())
            return;

        if (ev.NewMobState != MobState.Dead)
            return;

        if (!TryComp<SyndieBattleScoreComponent>(ev.Target, out var victimScore))
            return;

        var message = Loc.GetString("syndiebattle-kill-score", ("score", victimScore.Score));
        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, ev.Target, false, false);
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
}


