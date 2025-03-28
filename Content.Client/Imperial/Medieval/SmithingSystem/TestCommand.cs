using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.Imperial.Medieval.SmithingSystem;

public sealed class TestCommand : IConsoleCommand
{
    public string Command => "retardhelp";
    public string Description => "";
    public string Help => "";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ui = IoCManager.Resolve<IUserInterfaceManager>();

        var window = ui.CreateWindow<SmithingWindow>();
        window.OpenCentered();
    }
}
