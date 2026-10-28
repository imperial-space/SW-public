using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Boss;

[AdminCommand(AdminFlags.Admin)]
public sealed class BossCastCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public override string Command => "bosscast";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cancel = args.Length > 0 && args[0] == "cancel";
        if (args.Length == 0 || (cancel && args.Length != 2))
        {
            shell.WriteError(Help);
            return;
        }

        if (!TrySelectBoss(args[cancel ? 1 : 0], out var boss))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosscast_Select_Boss"));
            return;
        }

        var system = _entities.System<BossSystem>();
        if (cancel)
        {
            if (system.CancelBossCast(boss))
                shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosscast_Cancelled"));
            else
                shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosscast_No_Cast"));
            return;
        }

        var seconds = 3f;
        if (args.Length >= 2 && (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
                                !float.IsFinite(seconds) || seconds < 0.1f || seconds > 120f))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosscast_Duration"));
            return;
        }

        var name = args.Length >= 3 ? string.Join(" ", args.Skip(2)) : "Boss_Encounter_Cast_Preview_Name";
        if (name.Length > 128)
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosscast_Name_Too_Long"));
            return;
        }

        var attack = new BossTargetedAttack { CastName = name, CastTime = seconds };
        if (!system.TryStartBossCast(boss, attack, new[] { boss }, requireActiveEncounter: false))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosscast_Unavailable"));
            return;
        }

        shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosscast_Started"));
    }

    private bool TrySelectBoss(string selector, out EntityUid uid)
    {
        uid = default;
        if (NetEntity.TryParse(selector, out var netId))
        {
            if (!_entities.TryGetEntity(netId, out var entity) || entity is not { } found ||
                !_entities.HasComponent<BossComponent>(found))
                return false;

            uid = found;
            return true;
        }

        var matches = _entities.AllEntities<BossComponent>()
            .Where(ent => _entities.GetComponent<MetaDataComponent>(ent.Owner).EntityPrototype?.ID == selector)
            .Select(ent => ent.Owner).Take(2).ToArray();
        if (matches.Length != 1)
            return false;

        uid = matches[0];
        return true;
    }
}
