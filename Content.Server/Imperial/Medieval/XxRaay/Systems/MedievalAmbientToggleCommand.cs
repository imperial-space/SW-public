using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.XxRaay.Systems;

[AdminCommand(AdminFlags.Spawn)]
public sealed class MedievalAmbientToggleCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;

    public override string Command => "medievalambient";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var toggleSystem = _entitySystems.GetEntitySystem<MedievalAmbientToggleSystem>();
        bool? setEnabled = null;

        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "on":
                    setEnabled = true;
                    break;
                case "off":
                    setEnabled = false;
                    break;
                case "toggle":
                    break;
                default:
                    shell.WriteError("Usage: medievalambient <on|off|toggle>");
                    return;
            }
        }
        else
        {
            setEnabled = !toggleSystem.IsMedievalAmbientEnabled;
        }

        bool newState = setEnabled.HasValue
            ? toggleSystem.SetMedievalAmbientEnabled(setEnabled.Value)
            : toggleSystem.ToggleMedievalAmbient();

        shell.WriteLine(newState ? "Medieval ambient music enabled." : "Medieval ambient music disabled.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(new[] { "on", "off", "toggle" });
        return CompletionResult.Empty;
    }
}
