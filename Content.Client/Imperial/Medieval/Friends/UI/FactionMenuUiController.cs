using Content.Shared.Friends;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Imperial.Medieval.Friends.UI;

[UsedImplicitly]
public sealed class FactionMenuUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private FactionMenu? _menu;
    private FactionRemoveConfirmationMenu? _confirm;

    public void ToggleMenu(Dictionary<NetEntity, FactionMemberData> data)
    {
        if (_menu == null)
        {
            // setup window
            _menu = UIManager.CreateWindow<FactionMenu>();
            _menu.OnClose += () => _menu = null;

            _menu.ObjectiveSet += ObjectiveSet;
            _menu.GroupSet += GroupSet;
            _menu.RemoveButtonPressed += OpenConfirmationMenu;

            _menu.Populate(data);
            _menu.OpenCentered();
        }
        else
        {
            _menu.ObjectiveSet -= ObjectiveSet;
            _menu.GroupSet -= GroupSet;
            _menu.RemoveButtonPressed -= OpenConfirmationMenu;

            _menu.Dispose();
            _menu = null;
        }
    }

    public void PopulateMenu(Dictionary<NetEntity, FactionMemberData> data)
    {
        _menu?.Populate(data);
    }

    private void OpenConfirmationMenu(NetEntity ent)
    {
        _confirm?.Dispose();
        _confirm = new FactionRemoveConfirmationMenu(ent);
        _confirm.OnConfirm += args => _entityManager.RaisePredictiveEvent(new RemoveFactionMemberMessage(ent));
    }
    private void ObjectiveSet(NetEntity ent, string obj)
    {
        _entityManager.RaisePredictiveEvent(new SetFactionMemberObjectiveMessage(ent, obj));
    }
    private void GroupSet(NetEntity ent, string obj)
    {
        _entityManager.RaisePredictiveEvent(new SetFactionMemberGroupMessage(ent, obj));
    }
}
