using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Traitor;
using Content.Server.Traitor.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Players;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Imperial.XxRaay.SyndieBattle;

/// <summary>
/// Система для управления правилом игры SyndieBattle
/// </summary>
public sealed class SyndieBattleRuleSystem : GameRuleSystem<SyndieBattleRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorSystem = default!;
    [Dependency] private readonly UplinkSystem _uplinkSystem = default!;

    /// <summary>
    /// Инициализация системы
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    /// <summary>
    /// Вызывается при запуске правила игры
    /// </summary>
    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Active = true;

        // Конвертируем всех текущих игроков при запуске правила
        ConvertAllCurrentPlayers(component);
    }

    /// <summary>
    /// Вызывается при завершении правила игры
    /// </summary>
    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        component.Active = false;
    }

    /// <summary>
    /// Добавляет текст в сводку в конце раунда
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString("syndiebattle-round-end-result"));
    }

    /// <summary>
    /// Обрабатывает событие завершения спавна игрока
    /// </summary>
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // Проверяем, активно ли правило SyndieBattle
        if (!AnyRuleActive())
            return;

        if (!TryComp<SyndieBattleRuleComponent>(GetActiveRule(), out var comp) || !comp.Active)
            return;

        MakeTraitor(ev.Mob, comp);
    }

    /// <summary>
    /// Проверяет, активно ли хотя бы одно правило SyndieBattle
    /// </summary>
    /// <returns>True, если хотя бы одно правило активно</returns>
    private bool AnyRuleActive()
    {
        var query = EntityQueryEnumerator<SyndieBattleRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает активное правило SyndieBattle
    /// </summary>
    /// <returns>EntityUid активного правила или EntityUid.Invalid, если правило не найдено</returns>
    private EntityUid? GetActiveRule()
    {
        var query = EntityQueryEnumerator<SyndieBattleRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                return uid;
        }

        return null;
    }

    /// <summary>
    /// Превращает игрока в предателя
    /// </summary>
    private void MakeTraitor(EntityUid player, SyndieBattleRuleComponent comp)
    {
        // Получаем активный компонент TraitorRuleComponent для передачи в MakeTraitor
        if (GetActiveRule() is { } traitorRule && Comp<TraitorRuleComponent>(traitorRule) is { } traitorComp)
        {
            _traitorSystem.MakeTraitor(player, traitorComp);
        }
        AssignTraitorObjectives(player);
        if (_mindSystem.TryGetMind(player, out var mindId, out var mind))
        {
            _roleSystem.MindAddRole(mindId, "MindRoleTraitor", mind);
            // Добавляем игрока в список предателей
            comp.TraitorMinds.Add(mindId);
        }
    }

    /// <summary>
    /// Превращает всех текущих игроков в предателей
    /// </summary>
    /// <param name="comp">Компонент правила SyndieBattle</param>
    private void ConvertAllCurrentPlayers(SyndieBattleRuleComponent comp)
    {
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out var actor))
        {
            MakeTraitor(uid, comp);
        }
    }

    /// <summary>
    /// Назначает цели предателя игроку
    /// </summary>
    /// <param name="player">Сущность игрока</param>
    private void AssignTraitorObjectives(EntityUid player)
    {
        // Проверяем, есть ли у игрока разум
        if (!_mindSystem.TryGetMind(player, out var mindId, out var mind))
            return;

        // Добавляем основную цель выживания
        _mindSystem.TryAddObjective(mindId, mind, "SyndieBattleSurviveObjective");

        // Параметры для дополнительных целей
        const float initialMaxDifficulty = 7f;
        const int maxObjectives = 5;
        const string objectiveGroup = "TraitorObjectiveGroupKill";

        var maxDifficulty = initialMaxDifficulty;
        var picked = 0;

        // Добавляем дополнительные цели убийства
        while (picked < maxObjectives && maxDifficulty > 0f)
        {
            var objective = GetRandomObjective(mindId, mind, objectiveGroup, maxDifficulty);
            if (objective is null)
                break;

            _mindSystem.AddObjective(mindId, mind, objective.Value);

            // Уменьшаем доступную сложность и увеличиваем счетчик
            if (TryComp<ObjectiveComponent>(objective.Value, out var objectiveComp))
            {
                maxDifficulty -= objectiveComp.Difficulty;
            }
            picked++;
        }
    }

    /// <summary>
    /// Получает случайную цель для игрока
    /// </summary>
    private EntityUid? GetRandomObjective(EntityUid mindId, MindComponent mind, string objectiveGroup, float maxDifficulty)
    {
        // TODO: Реализовать выбор случайной цели для разума без обращения к TraitorRuleSystem.
        // Пока возвращаем null, чтобы не было ошибки компиляции.
        return null;
    }
}
