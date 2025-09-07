using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Server.Station.Components;
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
using Content.Shared.CombatMode.Pacification;
using Content.Server.KillTracking;
using Content.Server.Pinpointer;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Content.Shared.Damage.Systems;

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
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
    }

    protected override void Started(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        component.Active = true;
    component.StartTime = Timing.CurTime.TotalSeconds;

        // Спавним 10 машин искупления в случайных местах на станции
        SpawnRedemptionMachines();

        // Сообщаем всем игрокам цель режима
        _chatManager.ChatMessageToAll(
            ChatChannel.Server,
            Loc.GetString("syndiebattle-mode-goal"),
            Loc.GetString("syndiebattle-mode-goal"),
            default,
            false,
            false);

        // Конвертируем всех текущих игроков при запуске правила
        ConvertAllCurrentPlayers(component);
        ApplyPacifismToAllPlayers(component);

        Timer.Spawn(TimeSpan.FromMinutes(30), () =>
        {
            var active = GetActiveRuleEntity();
            if (active == null)
                return;

            ForceEndSelf(active.Value);
        });
    }

    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
    RemovePacifismFromAllPlayers();
    }

    protected override void AppendRoundEndText(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        var players = new List<(string Name, int KillCount, int Score, int TC, float DurationSec, bool Alive)>();
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<SyndieBattleScoreComponent, MobStateComponent>();
        while (query.MoveNext(out var ent, out var score, out var mobState))
        {
            var name = MetaData(ent).EntityName;
            var killCount = score.KillCount;
            var points = score.Score;
            var tc = 0;
            if (TryComp<InventoryComponent>(ent, out var inv))
            {
                var enumSlots = _inventory.GetSlotEnumerator((ent, inv));
                while (enumSlots.NextItem(out var item, out var slotDef))
                {
                    if (item == default)
                        continue;

                    if (TryComp<StoreComponent>(item, out var store))
                    {
                        if (store.Balance.TryGetValue("Telecrystal", out var bal))
                            tc += (int)bal;
                    }
                }
            }
            if (TryComp<HandsComponent>(ent, out var hands))
            {
                foreach (var hand in hands.Hands.Values)
                {
                    if (hand.HeldEntity != null && TryComp<StoreComponent>(hand.HeldEntity.Value, out var store))
                    {
                        if (store.Balance.TryGetValue("Telecrystal", out var bal))
                            tc += (int)bal;
                    }
                }
            }

            float duration = 0f;
            if (score.SpawnTime > 0)
            {
                if (score.SurvivalTime > 0)
                {
                    duration = score.SurvivalTime;
                }
                else
                {
                    duration = (float)(now.TotalSeconds - score.SpawnTime);
                }
            }

            var alive = score.Alive && !_mobStateSystem.IsDead(ent, mobState);

            players.Add((name, killCount, points, tc, duration, alive));
        }

        var topTC = players.OrderByDescending(p => p.TC).ThenByDescending(p => p.Score).Take(3).ToList();
        var topKills = players.OrderByDescending(p => p.KillCount).ThenByDescending(p => p.Score).Take(3).ToList();
        var topSurvive = players.OrderByDescending(p => p.DurationSec).ThenByDescending(p => p.Score).Take(3).ToList();

        args.AddLine(Loc.GetString("syndiebattle-round-end-title"));
        args.AddLine("");
        args.AddLine(Loc.GetString("syndiebattle-top-tc-header"));
        for (int i = 0; i < topTC.Count; i++)
            args.AddLine(Loc.GetString("syndiebattle-top-tc-entry", ("rank", i + 1), ("name", topTC[i].Name), ("tc", topTC[i].TC)));
        args.AddLine("");
        args.AddLine(Loc.GetString("syndiebattle-top-kills-header"));
        for (int i = 0; i < topKills.Count; i++)
            args.AddLine(Loc.GetString("syndiebattle-top-kills-entry", ("rank", i + 1), ("name", topKills[i].Name), ("kills", topKills[i].KillCount)));
        args.AddLine("");
        args.AddLine(Loc.GetString("syndiebattle-top-survive-header"));
        for (int i = 0; i < topSurvive.Count; i++)
        {
            var dur = TimeSpan.FromSeconds(topSurvive[i].DurationSec);
            var timeStr = dur.ToString(@"hh\:mm\:ss");
            var statusKey = topSurvive[i].Alive ? "syndiebattle-survive-status-alive" : "syndiebattle-survive-status-dead";
            var status = Loc.GetString(statusKey);
            var line = Loc.GetString("syndiebattle-top-survive-entry", ("rank", i + 1), ("name", topSurvive[i].Name), ("time", timeStr), ("status", status));
            args.AddLine(line);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!AnyRuleActive())
            return;

        var activeRule = GetActiveRuleEntity();
        if (activeRule == null || !TryComp<SyndieBattleRuleComponent>(activeRule, out var component))
            return;

        MakeTraitor(ev.Mob, component);
        var now = Timing.CurTime.TotalSeconds;
        if (component.StartTime > 0 && now - component.StartTime <= component.PacifyDurationSeconds)
        {
            ApplyPacifism(ev.Mob, component);
        }
        else if (component.StartTime > 0 && now - component.StartTime > component.PacifyDurationSeconds)
        {
            ApplyPacifism(ev.Mob, component);

            var god = EntityManager.System<SharedGodmodeSystem>();
            god.EnableGodmode(ev.Mob);
            Timer.Spawn(TimeSpan.FromSeconds(90), () =>
            {
                RemCompDeferred<SyndieBattlePacifiedMarkerComponent>(ev.Mob);
                RemCompDeferred<PacifiedComponent>(ev.Mob);
                god.DisableGodmode(ev.Mob);
            });
        }

        if (ev.Player != null)
        {
            var client = ev.Player.Channel;
            if (client != null)
            {
                var msg = Loc.GetString("syndiebattle-mode-goal");
                var delay = TimeSpan.FromSeconds(5.0 + _random.NextDouble());
                Timer.Spawn(delay, () => _chatManager.ChatMessageToOne(ChatChannel.Server, msg, msg, EntityUid.Invalid, false, client));
            }
        }
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

    var scoreComp = EnsureComp<SyndieBattleScoreComponent>(player);
    if (scoreComp != null)
    {
        scoreComp.SpawnTime = Timing.CurTime.TotalSeconds;
        scoreComp.Alive = true;
        scoreComp.SurvivalTime = 0f;
    }
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

    private void ApplyPacifism(EntityUid ent, SyndieBattleRuleComponent component)
    {
        if (!TryComp<MobStateComponent>(ent, out var mob) || _mobStateSystem.IsDead(ent, mob))
            return;

        EnsureComp<PacifiedComponent>(ent);
        EnsureComp<SyndieBattlePacifiedMarkerComponent>(ent);
    }

    private void ApplyPacifismToAllPlayers(SyndieBattleRuleComponent component)
    {
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var actor, out var mind, out var mobState))
        {
            if (_mobStateSystem.IsDead(uid, mobState))
                continue;

            ApplyPacifism(uid, component);
        }

        Timer.Spawn(TimeSpan.FromSeconds(component.PacifyDurationSeconds), () =>
        {
            RemovePacifismFromAllPlayers();
            _chatManager.ChatMessageToAll(ChatChannel.Server, Loc.GetString("syndiebattle-pacify-ended"), Loc.GetString("syndiebattle-pacify-ended"), default, false, false);
        });
    }

    private void RemovePacifismFromAllPlayers()
    {
        var query = EntityQueryEnumerator<SyndieBattlePacifiedMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            RemComp<PacifiedComponent>(uid);
            RemComp<SyndieBattlePacifiedMarkerComponent>(uid);
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
                killerScore.KillCount++;
            }
        }

        if (!TryComp<SyndieBattleScoreComponent>(ev.Entity, out var victimScore))
            return;

        if (victimScore != null && victimScore.SpawnTime > 0 && victimScore.Alive)
        {
            var deathAt = Timing.CurTime.TotalSeconds;
            victimScore.SurvivalTime = (float)(deathAt - victimScore.SpawnTime);
            victimScore.Alive = false;
        }

        var killerName = GetKillerName(ev.Primary);
        var victimName = MetaData(ev.Entity).EntityName;
        var location = GetDeathLocation(ev.Entity);

        var message = Loc.GetString("syndiebattle-kill-detail",
            ("killer", killerName),
            ("victim", victimName),
            ("location", location));

        _chatManager.ChatMessageToAll(ChatChannel.Server, message, message, ev.Entity, false, false);

        TryEndIfOneLeft();
    }

    private void TryEndIfOneLeft()
    {
        if (!AnyRuleActive())
            return;

        var active = GetActiveRuleEntity();
        if (active == null || !TryComp<SyndieBattleRuleComponent>(active.Value, out var comp))
            return;

        var aliveCount = 0;
        var query = EntityQueryEnumerator<SyndieBattleScoreComponent, MobStateComponent>();
        while (query.MoveNext(out var ent, out var score, out var mobState))
        {
            if (score.Alive && !_mobStateSystem.IsDead(ent, mobState))
                aliveCount++;
        }

        if (aliveCount <= 1)
        {
            ForceEndSelf(active.Value);
        }
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
        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out var station, out var stationData))
        {
            var spawned = 0;
            var maxAttempts = ruleComp.RedemptionMachineCount * 5;
            var attempts = 0;
            while (spawned < ruleComp.RedemptionMachineCount && attempts < maxAttempts)
            {
                attempts++;
                if (!TryFindRandomTileOnStation((station, stationData), out var tile, out var grid, out var coords))
                    continue;
                var occupied = false;
                var blockedReason = string.Empty;

                if (TryComp<MapGridComponent>(grid, out var gridComp))
                {
                    var indices = _map.TileIndicesFor(grid, gridComp, coords);
                    var anchored = new List<EntityUid>();
                    _map.GetAnchoredEntities((grid, gridComp), indices, anchored);

                    foreach (var near in anchored)
                    {
                        if (near == grid)
                            continue;
                    }
                }

                if (!occupied)
                {
                    foreach (var near in _lookup.GetEntitiesInRange(coords, 0.2f, LookupFlags.StaticSundries))
                    {
                        if (near == grid)
                            continue;
                    }
                }
                if (occupied)
                {
                    continue;
                }
                Spawn("SyndieBattleRedemptionMachine", coords);
                spawned++;
            }
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

            case KillEnvironmentSource:
                return "Окружение";
        }

        return "Что-то";
    }



    /// <summary>
    /// Получает место смерти
    /// </summary>
    private string GetDeathLocation(EntityUid entity)
    {
    var location = _navMap.GetNearestBeaconString(entity);
    return string.IsNullOrEmpty(location) ? "неизвестном месте" : location;
    }
}


