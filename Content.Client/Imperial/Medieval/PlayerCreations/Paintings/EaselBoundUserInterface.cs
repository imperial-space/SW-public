using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Client.Player;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Paintings;

using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Atmos.Monitor.Components;
using Robust.Client.UserInterface;

public sealed class EaselBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private EaselWindow? _window;

    public EaselBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<EaselWindow>();
        _window.Open();
        _window.Canvas.OnClickUp += SendSave;
        _window.OnClose += Close;
        _window.OnSend += SendPainting;
    }

    private void SendSave()
    {
        if (_window == null)
            return;

        SendMessage(new EaselSaveMessage(_window.CanvasTexture));
    }

    private void SendPainting(string name, string description, string author)
    {
        Logger.Debug("send!");

        if (_window == null)
            return;

        Logger.Debug("send");

        var localSession = _playerManager.LocalSession;
        if (localSession == null)
            return;

        SendMessage(new EaselSendPaintingMessage(_window.CanvasTexture, name, description, author, localSession.UserId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not EaselBoundUserInterfaceState cast || _window == null)
            return;

        _window.Populate(cast);
    }
}
