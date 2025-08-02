using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Administration;
using Content.Server.Imperial.Medieval.Administration.Nrp;
using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server.Administration;
using Content.Server.Imperial.Medieval.Administration.Nrp;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Administration.Nrp;
using Robust.Server.Player;
using Robust.Shared.Configuration;


namespace Content.Server.Imperial.Medieval.PlayerCreations.Administration;

[AdminCommand(AdminFlags.Fun)]
public sealed class OpenCreationsPanelCommand : IConsoleCommand
{
    public string Command => "creationspanel";
    public string Description => "Opens the creations panel.";
    public string Help => $"Usage: {Command}";

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        var ui = new CreationsPanelEui();
        eui.OpenEui(ui, player);
    }
}
