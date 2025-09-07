using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using Content.Shared.Store.Components;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Server.Station.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
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
using Content.Shared.Mind.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Server.KillTracking;
using Content.Server.Pinpointer;
using Content.Server.GameTicking;
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

        SpawnRedemptionMachines();

        _chatManager.ChatMessageToAll(
            ChatChannel.Server,
            Loc.GetString("syndiebattle-mode-goal"),
            Loc.GetString("syndiebattle-mode-goal"),
            default,
            false,
            false);

        ConvertAllCurrentPlayers(component);

        var pacifyDuration = component.PacifyDurationSeconds;

        if (pacifyDuration > 0f)
        {
            Timer.Spawn(TimeSpan.FromSeconds(pacifyDuration),
                () =>
                {
                    _chatManager.ChatMessageToAll(
                        ChatChannel.Server,
                        Loc.GetString("syndiebattle-pacify-ended"),
                        Loc.GetString("syndiebattle-pacify-ended"),
                        default,
                        false,
                        false);
                }
            );
        }
    }

    protected override void Ended(EntityUid uid, SyndieBattleRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        component.Active = false;
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
                while (enumSlots.NextItem(out var item, out _))
                {
                    if (item == default)
                        continue;
                    if (!TryComp<StoreComponent>(item, out var uplink))
                        continue;

                    if (uplink.Balance.TryGetValue("Telecrystal", out var bal))
                        tc += (int)bal;
                }
            }
            if (TryComp<HandsComponent>(ent, out var hands))
            {
                foreach (var hand in hands.Hands.Values)
                {
                    if (hand.HeldEntity == null || !TryComp<StoreComponent>(hand.HeldEntity.Value, out var store))
                        continue;

                    if (store.Balance.TryGetValue("Telecrystal", out var bal))
                        tc += (int)bal;
                }
            }

            var duration = score switch
            {
                { SpawnTime: > 0, SurvivalTime: > 0 } => score.SurvivalTime,
                { SpawnTime: > 0 } => (float)(now.TotalSeconds - score.SpawnTime),
                _ => 0f,
            };

            var alive = score.Alive && !_mobStateSystem.IsDead(ent, mobState);

            players.Add((name, killCount, points, tc, duration, alive));
        }

        var topTc = players.OrderByDescending(p => p.TC).ThenByDescending(p => p.Score).Take(3).ToList();
        var topKills = players.OrderByDescending(p => p.KillCount).ThenByDescending(p => p.Score).Take(3).ToList();
        var topSurvive = players.OrderByDescending(p => p.DurationSec).ThenByDescending(p => p.Score).Take(3).ToList();

        args.AddLine(Loc.GetString("syndiebattle-round-end-title") + "/n");
        args.AddLine(Loc.GetString("syndiebattle-top-tc-header"));
        for (var i = 0; i < topTc.Count; i++)
        {
            args.AddLine(Loc.GetString("syndiebattle-top-tc-entry", ("rank", i + 1), ("name", topTc[i].Name), ("tc", topTc[i].TC)));
        }

        args.AddLine(Loc.GetString("syndiebattle-top-kills-header") + "/n");
        for (var i = 0; i < topKills.Count; i++)
        {
            args.AddLine(Loc.GetString("syndiebattle-top-kills-entry", ("rank", i + 1), ("name", topKills[i].Name), ("kills", topKills[i].KillCount)));
        }

        args.AddLine(Loc.GetString("syndiebattle-top-survive-header") + "/n");
        for (var i = 0; i < topSurvive.Count; i++)
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

        ApplyPacifism(ev.Mob);

        var client = ev.Player.Channel;
        var msg = Loc.GetString("syndiebattle-mode-goal");
        var delay = TimeSpan.FromSeconds(5.0 + _random.NextDouble());

        Timer.Spawn(delay, () => _chatManager.ChatMessageToOne(ChatChannel.Server, msg, msg, EntityUid.Invalid, false, client));
    }

    private void ApplyPacifism(
        EntityUid uid,
        float duration = 0f,
        float godmodeDuration = 0f)
    {
        var godSystem = EntityManager.System<SharedGodmodeSystem>();

        if (!TryComp<MobStateComponent>(uid, out var mob) || _mobStateSystem.IsDead(uid, mob))
            return;

        EnsureComp<PacifiedComponent>(uid);

        if (godmodeDuration > 0f)
            godSystem.EnableGodmode(uid);

        if (duration <= 0f)
            return;

        Timer.Spawn(TimeSpan.FromSeconds(duration),
            () =>
            {
                if (EntityManager.EntityExists(uid))
                    RemCompDeferred<PacifiedComponent>(uid);

                if (godmodeDuration > 0f)
                    godSystem.DisableGodmode(uid);

                if (_playerManager.TryGetSessionByEntity(uid, out var session))
                {
                    _chatManager.ChatMessageToOne(
                        ChatChannel.Server,
                        Loc.GetString("syndiebattle-pacify-ended"),
                        Loc.GetString("syndiebattle-pacify-ended"),
                        uid,
                        false,
                        session.Channel);
                }
            }
        );
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

        scoreComp.SpawnTime = Timing.CurTime.TotalSeconds;
        scoreComp.Alive = true;
        scoreComp.SurvivalTime = 0f;
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
                killerScore.KillCount++;
            }
        }

        if (!TryComp<SyndieBattleScoreComponent>(ev.Entity, out var victimScore))
            return;

        if (victimScore is { SpawnTime: > 0, Alive: true })
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

    private void ConvertAllCurrentPlayers(SyndieBattleRuleComponent component)
    {
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out _, out _, out var mobState))
        {
            if (_mobStateSystem.IsDead(uid, mobState))
                continue;

            MakeTraitor(uid, component);
        }
    }

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
    private string GetKillerName(KillSource source)
    {
        switch (source)
        {
            case KillPlayerSource player:
                if (!_playerManager.TryGetSessionById(player.PlayerId, out var session))
                    return "Неизвестный игрок";
                return session.AttachedEntity == null ? "Неизвестный игрок" : MetaData(session.AttachedEntity.Value).EntityName;

            case KillEnvironmentSource:
                return "Окружение";
        }

        return "Что-то";
    }

    private string GetDeathLocation(EntityUid entity)
    {
        var location = _navMap.GetNearestBeaconString(entity);
        return string.IsNullOrEmpty(location) ? "неизвестном месте" : location;
    }
}
