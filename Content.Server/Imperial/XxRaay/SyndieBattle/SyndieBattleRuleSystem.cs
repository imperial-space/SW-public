using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Shared.Mind;
using Content.Server.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Server.Traitor.Uplink;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Server.Roles;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

public sealed class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

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
        if (!_mind.TryGetMind(player, out var mindId, out var mind))
            return;
            
        if (component == null)
        {
            var activeRule = GetActiveRuleEntity();
            if (activeRule == null || !TryComp<SyndieBattleRuleComponent>(activeRule, out component))
                return;
        }
            
        // Добавляем разум в список предателей
        component.TraitorMinds.Add(mindId);

        // Добавляем роль обычного предателя
        if (!_role.MindHasRole<TraitorRoleComponent>(mindId))
            _role.MindAddRole(mindId, "MindRoleTraitor", silent: false);
        
        // Выдаем аплинк, если это настроено в компоненте
        if (component.GiveUplink)
        {
            var pda = _uplink.FindUplinkTarget(player);
            _uplink.AddUplink(player, 20, pda, component.GiveCodewords);
        }
        
        AssignTraitorObjectives(player);

        if (TryComp<ActorComponent>(player, out var actor))
        {
            var scoreComp = EnsureComp<SyndieBattleScoreComponent>(player);
            scoreComp.PlayerId = actor.PlayerSession.UserId;
        }
    }

    private void AssignTraitorObjectives(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out var mind))
            return;

        _mind.TryAddObjective(mindId, mind, "SyndieBattleSurviveObjective");

        var maxDifficulty = 7f;
        var picked = 0;
        while (picked < 5 && maxDifficulty > 0f)
        {
            var objective = _objectives.GetRandomObjective(mindId, mind, "TraitorObjectiveGroups", maxDifficulty);
            if (objective is null)
                break;

            _mind.AddObjective(mindId, mind, objective.Value);
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
        _chat.ChatMessageToAll(ChatChannel.Server, message, message, ev.Target, false, false);
    }

    private bool AnyRuleActive()
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
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
        
        while (query.MoveNext(out var uid, out var actor, out var mind, out var mobState))
        {
            // Пропускаем мертвых игроков
            if (_mobState.IsDead(uid, mobState))
                continue;
                
            // Делаем игрока предателем
            MakeTraitor(uid, component);
        }
    }
}


