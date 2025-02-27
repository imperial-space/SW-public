using Content.Server.GameTicking.Rules.Components;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.GameTicking.Components;

namespace Content.Server.GameTicking.Rules;

public sealed class TDMRuleSystem : GameRuleSystem<TDMRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawningEvent);
    }

    private void OnPlayerSpawningEvent(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<TDMRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var pirates, out var gameRule))
        {
            if (!SpawnMap(uid, pirates.Sector0, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 0");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector1, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 1");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector2, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 2");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector3, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 3");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector4, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 4");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector5, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 5");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector6, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 6");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector7, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 7");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector7Cave, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval cave 7");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector9, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 9");
                continue;
            }
            if (!SpawnMap(uid, pirates.Sector10, pirates))
            {
                Logger.InfoS("tdm", "Failed to load map for medieval 10");
                continue;
            }

        }
    }

    private bool SpawnMap(EntityUid uid, ResPath[] mappath, TDMRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var path = _random.Pick(mappath);

        var mapId = _mapManager.CreateMap();
        var options = new MapLoadOptions
        {
            LoadMap = true,
        };

        _map.TryLoad(mapId, path.ToString(), out var outpostGrids, options);

        return true;
    }
}


