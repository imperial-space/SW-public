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

    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "testboss";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var bosses = _entMan.AllEntities<BossComponent>().Select(x => x.Owner);
        if (args.Length == 1)
        {
            if (NetEntity.TryParse(args[0], out var netId))
            {
                _entMan.TryGetEntity(netId, out var selected);
                bosses = bosses.Where(uid => uid == selected);
            }
            else
            {
                bosses = bosses.Where(uid => _entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == args[0]);
            }
        }

        var matches = bosses.ToList();
        if (matches.Count == 0 && args.Length == 1 && TrySpawnLegacyBoss(args[0], out var spawned))
            matches.Add(spawned);
        if (matches.Count != 1)
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Testboss_Select"));
            return;
        }

        var boss = matches[0];
        var component = _entMan.GetComponent<BossComponent>(boss);
        var grid = _entMan.GetComponent<TransformComponent>(boss).GridUid;
        var players = _players.Sessions.Where(session => session.AttachedEntity != null).Select(session => session.AttachedEntity!.Value).Where(uid => _entMan.HasComponent<HumanoidAppearanceComponent>(uid)).Where(uid => component.TeleportPlayers || _entMan.GetComponent<TransformComponent>(uid).GridUid == grid).ToList();
        if (!_entMan.System<BossSystem>().TryStartBossfight(players, boss))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Testboss_Start_Failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Testboss_Started", ("boss", _entMan.GetComponent<MetaDataComponent>(boss).EntityName)));
    }

    private bool TrySpawnLegacyBoss(string id, out EntityUid boss)
    {
        boss = default;
        if (!_prototypeManager.TryIndex<EntityPrototype>(id, out var prototype) ||
            !prototype.Components.TryGetValue(_componentFactory.GetComponentName(typeof(BossComponent)), out var entry) ||
            prototype.Components.ContainsKey(_componentFactory.GetComponentName(typeof(BossEncounterComponent))))
            return false;
        var settings = (BossComponent)entry.Component;
        if (!_mapLoaderSystem.TryLoadMap(settings.MapPath, out var map, out _,
                new DeserializationOptions { InitializeMaps = true }))
            return false;
        var query = _entMan.EntityQueryEnumerator<BossSpawnComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var transform))
        {
            if (transform.MapUid != map.Value.Owner)
                continue;
            boss = _entMan.SpawnEntity(prototype.ID, transform.Coordinates);
            return true;
        }
        _entMan.DeleteEntity(map.Value.Owner);
        return false;
    }
}
