using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules.TraderSpawn;

[AdminCommand(AdminFlags.Debug)]
public sealed class SpawnTraderRuleCommand : IConsoleCommand
{
    private const string RulePrototype = "MedievalTraderSpawnRule";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public string Command => "spawntrader";
    public string Description => "Spawns a trader and transfers the command user into it.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { AttachedEntity: { } performer })
        {
            shell.WriteError("This command must be run through a client console while controlling an entity.");
            return;
        }

        var gameTicker = _entitySystemManager.GetEntitySystem<GameTicker>();
        var ruleUid = gameTicker.AddGameRule(RulePrototype);
        var rule = _entityManager.GetComponent<TraderSpawnRuleComponent>(ruleUid);
        rule.Performer = performer;
        gameTicker.StartGameRule(ruleUid);
    }
}
