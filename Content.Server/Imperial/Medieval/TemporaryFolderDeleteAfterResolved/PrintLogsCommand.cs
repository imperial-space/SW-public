using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Temporary
{
    [AdminCommand(AdminFlags.Debug)]
    public sealed class DeleteGasCommand : IConsoleCommand
    {
        [Dependency] private readonly IServerDbManager _db = default!;

        public string Command => "printdblogs";
        public string Description => "prints db logs";
        public string Help => "";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var logs = _db.GetDbLogs();
            foreach (var (name, value) in logs)
            {
                shell.WriteLine($"{name}: {value}");
            }
        }
    }

}
