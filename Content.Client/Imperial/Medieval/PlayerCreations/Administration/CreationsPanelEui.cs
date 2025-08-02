using System.Linq;
using System.Security.Principal;
using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Imperial.Medieval.Administration.Nrp;
using Content.Shared.Imperial.Medieval.PlayerCreations;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Administration;

public sealed class CreationsPanelEui : BaseEui
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill;

    private readonly Dictionary<CreationPaintingMessage, PaintingEntry> _invokingPaintings = new();

    private CreationsPanel? _creationsPanel;


    public CreationsPanelEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("creations.panel");
        _creationsPanel = new CreationsPanel();
        _creationsPanel.OnClose += OnCloseWindow;
    }

    private void OnCloseWindow()
    {
        SendMessage(new CloseEuiMessage());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        switch (msg)
        {
            case ResponseIncomingCreationPaintingsMessage responseIncoming:
                foreach (var painting in responseIncoming.Paintings)
                {
                    TryCreateIncoming(painting);
                }
                break;
            case NewIncomingCreationPaintingMessage newIncoming:
                TryCreateIncoming(newIncoming.Painting);
                break;
        }
    }

    private bool TryCreateIncoming(CreationPaintingMessage painting)
    {
        Logger.Debug("Trying lol");

        if (_invokingPaintings.ContainsKey(painting))
            return false;
        var entry = new PaintingEntry(painting);
        _invokingPaintings.Add(painting, entry);
        _creationsPanel?.IncomingPaintingsTab.AddEntry(entry);
        Logger.Debug("goddamn");

        return true;
    }

    public override void Opened()
    {
        base.Opened();

        _creationsPanel?.OpenCenteredLeft();
        //RequestMessages();
        _sawmill.Debug("Hi!");
        SendMessage(new RequestIncomingCreationPaintingsMessage());
    }

    public override void Closed()
    {
        base.Closed();

        _creationsPanel?.Dispose();
    }
}
