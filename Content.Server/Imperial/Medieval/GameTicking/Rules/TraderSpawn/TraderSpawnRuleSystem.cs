using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules.TraderSpawn;

public sealed class TraderSpawnRuleSystem : GameRuleSystem<TraderSpawnRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    protected override void Started(
        EntityUid uid,
        TraderSpawnRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Performer is not { } performer ||
            TerminatingOrDeleted(performer) ||
            !_players.TryGetSessionByEntity(performer, out var session) ||
            !_mind.TryGetMind(performer, out var mindId, out var mind) ||
            !_transform.TryGetMapOrGridCoordinates(performer, out var coordinates))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var station = _station.GetOwningStation(performer);
        var profile = GameTicker.GetPlayerProfile(session);
        var trader = _spawning.SpawnPlayerMob(coordinates.Value, component.Job, profile, station);

        _transform.SetCoordinates(trader, coordinates.Value);
        _transform.AttachToGridOrMap(trader);
        _mind.TransferTo(mindId, trader, true, mind: mind);
        _roles.MindAddJobRole(mindId, mind, false, component.Job);

        var spawnEvent = new PlayerSpawnCompleteEvent(
            trader,
            session,
            component.Job,
            false,
            false,
            GameTicker.PlayersJoinedRoundNormally,
            station ?? EntityUid.Invalid,
            profile);

        RaiseLocalEvent(trader, spawnEvent, true);

        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(performer):player} used {args.RuleId} and transferred to {ToPrettyString(trader):target} as {component.Job}.");

        ForceEndSelf(uid, gameRule);
    }
}
