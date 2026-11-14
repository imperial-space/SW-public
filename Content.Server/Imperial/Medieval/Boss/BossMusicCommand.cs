using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[AdminCommand(AdminFlags.Admin)]
public sealed class BossMusicCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    public override string Command => "bossmusic";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 1 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var profile in _prototypes.EnumeratePrototypes<BossSoundtrackPrototype>().OrderBy(profile => profile.ID))
                shell.WriteLine(Loc.GetString("Boss_Soundtrack_Command_Entry", ("id", profile.ID),
                    ("name", Loc.GetString(profile.Name))));
            return;
        }
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Help);
            return;
        }
        var id = new ProtoId<BossSoundtrackPrototype>(args[0].ToLowerInvariant());
        if (!_prototypes.TryIndex(id, out var soundtrack))
        {
            shell.WriteError(Loc.GetString("Boss_Soundtrack_Command_Unknown"));
            return;
        }
        if (!TrySelectArena(shell, args.Length == 2 ? args[1] : null, out var arena))
        {
            shell.WriteError(Loc.GetString("Boss_Soundtrack_Command_Select_Arena"));
            return;
        }
        if (!_entities.System<BossMusicSystem>().TrySetSoundtrack(arena, id))
        {
            shell.WriteError(Loc.GetString("Boss_Soundtrack_Command_Unavailable"));
            return;
        }
        shell.WriteLine(Loc.GetString("Boss_Soundtrack_Command_Selected", ("name", Loc.GetString(soundtrack.Name))));
    }

    private bool TrySelectArena(IConsoleShell shell, string? selector, out EntityUid arena)
    {
        arena = default;
        if (selector == null && shell.Player?.AttachedEntity is { } player &&
            _entities.GetComponent<TransformComponent>(player).MapUid is { } playerMap &&
            _entities.HasComponent<BossArenaMapComponent>(playerMap))
        {
            arena = playerMap;
            return true;
        }
        if (selector != null && NetEntity.TryParse(selector, out var net) &&
            _entities.TryGetEntity(net, out var entity) && entity is { } uid)
        {
            if (_entities.HasComponent<BossArenaMapComponent>(uid)) arena = uid;
            else if (_entities.GetComponent<TransformComponent>(uid).MapUid is { } map &&
                _entities.HasComponent<BossArenaMapComponent>(map)) arena = map;
            else return false;
            return true;
        }
        var matches = _entities.AllEntities<BossArenaMapComponent>()
            .Where(entity => selector == null || entity.Comp.ArenaId == selector).Take(2).ToArray();
        if (matches.Length != 1) return false;
        arena = matches[0].Owner;
        return true;
    }
}
