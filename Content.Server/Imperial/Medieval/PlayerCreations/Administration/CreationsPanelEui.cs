using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Server.Administration;
using Content.Shared.Imperial.Medieval.Administration.Nrp;
using Content.Shared.Imperial.Medieval.PlayerCreations;

namespace Content.Server.Imperial.Medieval.PlayerCreations.Administration;

public sealed class CreationsPanelEui : BaseEui
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private readonly CreationsSystem _creationsSystem;
    private readonly ISawmill _sawmill;

    public CreationsPanelEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = Logger.GetSawmill("CreationsPanelEui");
        _creationsSystem = _entitySystemManager.GetEntitySystem<CreationsSystem>();
    }
    public override void Opened()
    {
        base.Opened();
        _creationsSystem.RegisterEui(this);
        _adminManager.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();

        _creationsSystem.UnregisterEui(this);
        _adminManager.OnPermsChanged -= OnPermsChanged;
    }


    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_adminManager.HasAdminFlag(Player, AdminFlags.Moderator))
        {
            Close();
        }
    }

    public void SendNewPainting(CreationPaintingMessage painting)
    {
        Logger.Debug("New painting");

        SendMessage(new NewIncomingCreationPaintingMessage(painting));
    }

    public void SendRemovePainting(CreationPaintingMessage painting)
    {
        SendMessage(new RemoveIncomingCreationPaintingMessage(painting));
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Fun))
            return;


        switch (msg)
        {
            case RequestIncomingCreationPaintingsMessage:
                SendMessage(new ResponseIncomingCreationPaintingsMessage(_creationsSystem.GetInvokingPaintings()));
                break;
            case RemoveIncomingCreationPaintingMessage remove:
                _creationsSystem.RemoveInvokingPainting(remove.Painting);
                break;
        }
    }


}
