using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Boss;

[AdminCommand(AdminFlags.Admin)]
public sealed class BossTelegraphCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    public override string Command => "bosstelegraph";
    private static readonly Dictionary<string, BossTelegraphPattern> Patterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sinistra"] = BossTelegraphPattern.Sinistra,
        ["dextra"] = BossTelegraphPattern.Dextra,
        ["frons"] = BossTelegraphPattern.Frons,
        ["tergum"] = BossTelegraphPattern.Tergum,
        ["occidens"] = BossTelegraphPattern.Sinistra,
        ["oriens"] = BossTelegraphPattern.Dextra,
        ["septentrio"] = BossTelegraphPattern.Frons,
        ["meridies"] = BossTelegraphPattern.Tergum,
        ["west"] = BossTelegraphPattern.Sinistra,
        ["east"] = BossTelegraphPattern.Dextra,
        ["north"] = BossTelegraphPattern.Frons,
        ["south"] = BossTelegraphPattern.Tergum,
        ["blackout-nw"] = BossTelegraphPattern.BlackoutNorthWest,
        ["blackout-ne"] = BossTelegraphPattern.BlackoutNorthEast,
        ["blackout-sw"] = BossTelegraphPattern.BlackoutSouthWest,
        ["blackout-se"] = BossTelegraphPattern.BlackoutSouthEast,
        ["rejection"] = BossTelegraphPattern.Rejection,
        ["invitation"] = BossTelegraphPattern.Invitation,
        ["cross"] = BossTelegraphPattern.Cross,
        ["divide"] = BossTelegraphPattern.Divide,
        ["crystal-small"] = BossTelegraphPattern.CrystalRainSmall,
        ["crystal-large"] = BossTelegraphPattern.CrystalRain,
        ["spread"] = BossTelegraphPattern.Spread,
        ["stack"] = BossTelegraphPattern.Stack,
        ["blue-first"] = BossTelegraphPattern.BlueFirst,
        ["purple-first"] = BossTelegraphPattern.PurpleFirst,
        ["severance"] = BossTelegraphPattern.Spread,
        ["communion"] = BossTelegraphPattern.Stack,
        ["elegy"] = BossTelegraphPattern.BlueFirst,
        ["dirge"] = BossTelegraphPattern.PurpleFirst
    };

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3 || !TrySelectBoss(args[0], out var boss))
        {
            shell.WriteError(Help);
            return;
        }
        var bosses = _entities.System<BossSystem>();
        var telegraphs = _entities.System<BossTelegraphSystem>();
        var focus = _entities.System<BossFocusMarkerSystem>();
        if (args[1] == "clear")
        {
            bosses.CancelBossCast(boss);
            telegraphs.Clear(boss);
            focus.Clear(boss);
            shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Cleared"));
            return;
        }
        if (args[1] == "tank")
        {
            var player = shell.Player?.AttachedEntity;
            if (args.Length == 3)
                player = NetEntity.TryParse(args[2], out var netId) && _entities.TryGetEntity(netId, out var found) ? found : null;
            if (player is not { } body || !focus.SetTank(boss, body))
                shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Tank_Failed"));
            else
                shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Tank_Set"));
            return;
        }
        if (!Patterns.TryGetValue(args[1], out var pattern))
        {
            shell.WriteError(Help);
            return;
        }
        var seconds = BossTilePatterns.IsTwoPhase(pattern) || BossTilePatterns.IsBlackout(pattern) ? 6f : 5f;
        if (args.Length == 3 && (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
            !float.IsFinite(seconds) || seconds < 1 || seconds > 120))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Duration"));
            return;
        }
        BossAttack attack;
        IEnumerable<EntityUid> targets;
        if (pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack)
        {
            var players = telegraphs.LivingPlayers(boss);
            var count = pattern == BossTelegraphPattern.Spread ? 4 : 1;
            attack = new PlayerTargetedAttack { TargetLimits = (count, count + 1) };
            targets = players;
        }
        else
        {
            attack = new BossTargetedAttack();
            targets = new[] { boss };
        }
        attack.Telegraph = pattern;
        attack.CastTime = seconds;
        attack.CastName = _entities.System<BossMechanicSystem>().CastNameKey(boss, pattern);
        if (!bosses.TryStartBossCast(boss, attack, targets, requireActiveEncounter: false))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Unavailable"));
            return;
        }
        shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosstelegraph_Started"));
    }

    private bool TrySelectBoss(string selector, out EntityUid boss)
    {
        boss = default;
        if (NetEntity.TryParse(selector, out var id))
        {
            if (!_entities.TryGetEntity(id, out var entity) || entity is not { } uid || !_entities.HasComponent<BossTelegraphSettingsComponent>(uid))
                return false;
            boss = uid;
            return true;
        }
        var matches = _entities.AllEntities<BossTelegraphSettingsComponent>()
            .Where(ent => _entities.GetComponent<MetaDataComponent>(ent.Owner).EntityPrototype?.ID == selector).Take(2).ToArray();
        if (matches.Length != 1)
            return false;
        boss = matches[0].Owner;
        return true;
    }
}
