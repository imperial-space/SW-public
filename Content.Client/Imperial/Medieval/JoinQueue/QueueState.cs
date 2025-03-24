using Content.Shared.Imperial.Medieval.JoinQueue;
using Robust.Client.Audio;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Medieval.JoinQueue;

public sealed class QueueState : State, IPostInjectInit
{
    private const string JoinSoundPath = "/Audio/Effects/voteding.ogg";
    private const string QuitCommand = "quit";

    [Dependency] private readonly IUserInterfaceManager _userInterface = default!;
    [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private AudioSystem? _audio;
    private QueueControl? _gui;

    public void PostInject()
    {
        _entity.TrySystem(out _audio);
    }

    protected override void Startup()
    {
        _gui = new QueueControl();
        _userInterface.StateRoot.AddChild(_gui);

        _gui.QuitPressed += OnQuitPressed;
    }

    protected override void Shutdown()
    {
        _gui!.QuitPressed -= OnQuitPressed;
        _gui.Dispose();

        Ding();
    }

    private void Ding()
    {
        _audio?.PlayGlobal(JoinSoundPath, Filter.Local(), false);
    }

    public void OnQueueUpdate(MsgQueueUpdate msg)
    {
        _gui?.UpdateInfo(msg.Total, msg.Position);
    }

    private void OnQuitPressed()
    {
        _consoleHost.ExecuteCommand(QuitCommand);
    }
}
