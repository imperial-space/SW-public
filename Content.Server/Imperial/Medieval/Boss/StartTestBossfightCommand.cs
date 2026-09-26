using System.Linq;
using Content.Server.Administration.Systems;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class StartTestBossfightCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    public override string Command => "testboss";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("No prototype specified.");
            return;
        }

        if (!_prototypeManager.TryIndex<EntityPrototype>(args[0], out var proto))
        {
            shell.WriteError($"Prototype {args[0]} not found.");
            return;
        }

        string compName = _componentFactory.GetComponentName(typeof(BossComponent));

        if (!proto.Components.TryGetValue(compName, out var registration))
        {
            shell.WriteError($"Prototype {proto.ID} does not have BossComponent.");
            return;
        }

        var bossComponent = (BossComponent)registration.Component;

        var options = new DeserializationOptions
        {
            InitializeMaps = true,
        };

        if (!_mapLoaderSystem.TryLoadMap(bossComponent.MapPath, out var map, out var _, options))
        {
            shell.WriteError($"Failed to load map: {bossComponent.MapPath}");
            return;
        }

        var bossSpawnQuery = _entMan.EntityQueryEnumerator<BossSpawnComponent, TransformComponent>();
        EntityCoordinates spawnCoords = default;
        bool foundSpawn = false;

        while (bossSpawnQuery.MoveNext(out var uid, out var _, out var xform))
        {
            spawnCoords = xform.Coordinates;
            foundSpawn = true;
            break;
        }

        if (!foundSpawn)
        {
            shell.WriteError("BossSpawnComponent not found on the loaded map.");
            return;
        }

        _entMan.SpawnEntity(proto.ID, spawnCoords);

        var players = _entMan.AllEntities<HumanoidAppearanceComponent>().Select(x => x.Owner).ToList();
        var boss = _entMan.AllEntities<BossComponent>().First();

        _entMan.System<BossSystem>().StartBossfight(players, boss);
    }
}
