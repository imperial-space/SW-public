using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Toggles a private diagnostic line for the local player's current lock.</summary>
[AnyCommand]
public sealed class MeleeLockOnDebugCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "melee_lockon_debug";
    public string Description => "Draw the local lock-on line: green = good, yellow = lock timeout, red = camera tolerance.";
    public string Help => "melee_lockon_debug [on|off] — no argument toggles the line.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var tracking = _systems.GetEntitySystem<MeleeLockOnTrackingSystem>();
        bool? enabled = args.Length == 0 ? !tracking.DebugEnabled : args[0].ToLowerInvariant() switch
        {
            "on" or "true" or "1" => true,
            "off" or "false" or "0" => false,
            _ => null,
        };
        if (enabled is not { } value)
        {
            shell.WriteError(Help);
            return;
        }

        tracking.DebugEnabled = value;
        shell.WriteLine($"Melee lock-on debug: {(value ? "on" : "off")}.");
    }
}
