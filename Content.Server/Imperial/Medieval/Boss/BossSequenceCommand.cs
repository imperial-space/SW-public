using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Boss;

[AdminCommand(AdminFlags.Admin)]
public sealed class BossSequenceCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    public override string Command => "bosssequence";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3 || !TrySelectBoss(args[0], out var boss))
        {
            shell.WriteError(Help);
            return;
        }
        var system = _entities.System<BossSequenceSystem>();
        var selection = args.Length >= 2 ? args[1].ToLowerInvariant() : "random";
        if (selection == "cancel" && args.Length == 2)
        {
            if (system.Cancel(boss)) shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosssequence_Cancelled"));
            else shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosssequence_No_Sequence"));
            return;
        }
        var interval = (args.Length == 3 ? args[2].ToLowerInvariant() : "normal") switch
        {
            "normal" => 4f,
            "slow" => 5f,
            "fast" => 2f,
            _ => 0f
        };
        List<BossTelegraphPattern>? queue = null;
        if (selection != "random")
        {
            queue = new();
            foreach (var word in selection.Split(','))
            {
                var pattern = word.Trim() switch
                {
                    "occidens" or "west" or "left" or "sinistra" => BossTelegraphPattern.Sinistra,
                    "oriens" or "east" or "right" or "dextra" => BossTelegraphPattern.Dextra,
                    "septentrio" or "north" or "front" or "frons" => BossTelegraphPattern.Frons,
                    "meridies" or "south" or "back" or "tergum" => BossTelegraphPattern.Tergum,
                    _ => (BossTelegraphPattern?)null
                };
                if (pattern == null)
                {
                    shell.WriteError(Help);
                    return;
                }
                queue.Add(pattern.Value);
            }
        }
        if (interval == 0 || queue != null && !BossSequenceSystem.ValidQueue(queue))
        {
            shell.WriteError(Help);
            return;
        }
        var attack = _entities.GetComponent<BossComponent>(boss).Stages.OrderBy(stage => stage.Key)
            .SelectMany(stage => stage.Value.Attacks).OfType<BossPreparedSequenceAttack>().FirstOrDefault();
        if (attack == null || !system.TryStart(boss, attack, preview: true, queue: queue, interval: interval))
        {
            shell.WriteError(Loc.GetString("Boss_Encounter_Command_Bosssequence_Unavailable"));
            return;
        }
        var sequence = _entities.GetComponent<BossSequenceComponent>(boss);
        shell.WriteLine(Loc.GetString("Boss_Encounter_Command_Bosssequence_Started", ("count", sequence.Queue.Count), ("interval", interval),
            ("preload", attack.PreloadTime), ("release", attack.ReleaseTime)));
    }

    private bool TrySelectBoss(string selector, out EntityUid boss)
    {
        boss = default;
        if (NetEntity.TryParse(selector, out var id))
        {
            if (!_entities.TryGetEntity(id, out var entity) || entity is not { } uid ||
                !_entities.HasComponent<BossTelegraphSettingsComponent>(uid)) return false;
            boss = uid;
            return true;
        }
        var matches = _entities.AllEntities<BossTelegraphSettingsComponent>()
            .Where(ent => _entities.GetComponent<MetaDataComponent>(ent.Owner).EntityPrototype?.ID == selector).Take(2).ToArray();
        if (matches.Length != 1) return false;
        boss = matches[0].Owner;
        return true;
    }
}
