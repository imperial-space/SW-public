using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Boss;

[AdminCommand(AdminFlags.Admin)]
public sealed class BossArenaGateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public override string Command => "bossgate";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || (args[0] == "link" ? args.Length != 3 : args.Length != 2) ||
            args[0] is not ("link" or "open" or "close"))
        {
            shell.WriteError(Help);
            return;
        }

        if (!TrySelect<BossArenaGateComponent>(args[1], out var gate))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bossgate_Select_Gate"));
            return;
        }

        var system = _entities.System<BossArenaGateSystem>();
        if (args[0] == "link")
        {
            if (!TrySelect<BossArenaEntranceComponent>(args[2], out var entrance) || !system.LinkGate(gate, entrance))
            {
                shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bossgate_Select_Entrance"));
                return;
            }
            shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bossgate_Linked"));
            return;
        }

        if (!system.SetOpen(gate, args[0] == "open"))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bossgate_Needs_Entrance"));
            return;
        }

        shell.WriteLine(Loc.GetString(args[0] == "open" ? "Boss_Encounter_Command_Bossgate_Opened" : "Boss_Encounter_Command_Bossgate_Closed"));
    }

    private bool TrySelect<T>(string selector, out EntityUid uid) where T : Component
    {
        uid = default;
        if (NetEntity.TryParse(selector, out var netId))
        {
            if (!_entities.TryGetEntity(netId, out var entity) || entity is not { } found || !_entities.HasComponent<T>(found))
                return false;
            uid = found;
            return true;
        }

        var matches = _entities.AllEntities<T>()
            .Where(ent => _entities.GetComponent<MetaDataComponent>(ent.Owner).EntityPrototype?.ID == selector)
            .Select(ent => ent.Owner).Take(2).ToArray();
        if (matches.Length != 1)
            return false;
        uid = matches[0];
        return true;
    }
}
