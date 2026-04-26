using System.Linq;
using Content.Server.Administration;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Achievements;

[AdminCommand(AdminFlags.Admin)]
public sealed class AchievementCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    public string Command => "achievement";
    public string Description => "Управление достижениями игроков: grant, revoke, get";
    public string Help => "Использование: achievement <grant|revoke|get> <имя игрока> [id достижения]";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var action = args[0].ToLower();
        var playerName = args[1];
        
        var locator = IoCManager.Resolve<IPlayerLocator>();
        var located = await locator.LookupIdByNameOrIdAsync(playerName);

        if (located == null)
        {
            shell.WriteError($"Игрок '{playerName}' не найден.");
            return;
        }

        var guid = located.UserId.UserId;
        var achSystem = EntitySystem.Get<AchievementSystem>();

        switch (action)
        {
            case "grant":
                if (args.Length < 3)
                {
                    shell.WriteError("Укажите ID достижения.");
                    return;
                }

                var grantId = args[2];
                
                _playerManager.TryGetSessionById(located.UserId, out var session);
                
                var granted = await achSystem.TryGrantAchievement(guid, grantId, session);
                
                if (granted)
                    shell.WriteLine($"Достижение '{grantId}' успешно выдано игроку {playerName}.");
                else
                    shell.WriteError($"Не удалось выдать достижение.");
                break;

            case "revoke":
                if (args.Length < 3)
                {
                    shell.WriteError("Укажите ID достижения.");
                    return;
                }
                var revokeId = args[2];
                var revoked = await achSystem.TryRevokeAchievement(guid, revokeId);
                
                if (revoked)
                    shell.WriteLine($"Достижение '{revokeId}' удалено у игрока {playerName}.");
                else
                    shell.WriteError($"Не удалось удалить достижение.");
                break;

            case "get":
                var achievements = achSystem.GetUnlockedAchievements(guid);
                
                if (achievements.Count == 0)
                    shell.WriteLine($"У игрока {playerName} нет открытых достижений.");
                else
                    shell.WriteLine($"Достижения {playerName}: {string.Join(", ", achievements)}");
                break;

            default:
                shell.WriteError($"Неизвестное действие: {action}.");
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(new[] { "grant", "revoke", "get" }, "Выберите действие");
        }

        if (args.Length == 2)
        {
            var names = _playerManager.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(names, _loc.GetString("shell-argument-username-optional-hint"));
        }

        if (args.Length == 3 && (args[0] == "grant" || args[0] == "revoke"))
        {
            var proto = IoCManager.Resolve<IPrototypeManager>();
            var options = proto.EnumeratePrototypes<AchievementPrototype>()
                .Select(p => p.ID)
                .OrderBy(id => id);
                
            return CompletionResult.FromHintOptions(options, "ID достижения");
        }

        return CompletionResult.Empty;
    }
}
