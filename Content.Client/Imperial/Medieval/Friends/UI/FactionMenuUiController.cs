using Content.Shared.Friends;
using Content.Shared.Friends.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Friends.UI;

[UsedImplicitly]
public sealed class FactionMenuUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private FactionMenu? _menu;

    public void ToggleMenu()
    {
        if (_menu == null)
        {
            // setup window
            _menu = UIManager.CreateWindow<FactionMenu>();
            _menu.OnClose += () => _menu = null;

            _menu.ObjectiveSet += ObjectiveSet;
            _menu.GroupSet += GroupSet;
            _menu.RemoveButtonPressed += OpenConfirmationMenu;

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

    public void PopulateMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<NetEntity, FactionMemberData> data)
    {
        _menu?.Populate(proto, data);
    }

    private void OpenConfirmationMenu(NetEntity ent)
    {
    }

    private void ObjectiveSet(FactionMemberGroup group, string obj)
    {
        if (_menu == null)
            return;

        _entityManager.RaisePredictiveEvent(new SetFactionMemberObjectiveMessage(_menu.Faction, group, obj));
    }
    private void GroupSet(NetEntity ent, FactionMemberGroup obj)
    {
        _entityManager.RaisePredictiveEvent(new SetFactionMemberGroupMessage(ent, obj));
    }
}
