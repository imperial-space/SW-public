using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Player;
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
            _menu.SetLeaderPressed += SetLeader;
            _menu.FirePressed += args => Fire(args, "", false);
            _menu.HeadhuntPressed += (id, details) => Fire(id, details, true);

            _menu.OpenCentered();
        }
        else
        {
            _menu.ObjectiveSet -= ObjectiveSet;
            _menu.GroupSet -= GroupSet;
            _menu.SetLeaderPressed -= SetLeader;
            _menu.FirePressed -= args => Fire(args, "", false);
            _menu.HeadhuntPressed -= (id, details) => Fire(id, details, true);

            _menu.Dispose();
            _menu = null;
        }
    }

    public void PopulateMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data, FactionMenuAccess access, FactionMemberGroup selfGroup, int self)
    {
        _menu?.Populate(proto, self, data, access, selfGroup);
    }

    private void Fire(int ent, string details, bool headhunt = false)
    {
        var playerMan = IoCManager.Resolve<IPlayerManager>();
        if (_entityManager.TryGetComponent<FriendsComponent>(playerMan.LocalEntity, out var friends))
            _entityManager.RaisePredictiveEvent(new RemoveFactionMemberMessage(ent, friends.MemberID, details, headhunt));
    }

    private void ObjectiveSet(FactionMemberGroup group, string obj)
    {
        if (_menu == null)
            return;

        _entityManager.RaisePredictiveEvent(new SetFactionMemberObjectiveMessage(_menu.Faction, group, obj));
    }
    private void GroupSet(int ent, FactionMemberGroup obj)
    {
        _entityManager.RaisePredictiveEvent(new SetFactionMemberGroupMessage(ent, obj));
    }

    private void SetLeader(int ent, bool leader)
    {
        _entityManager.RaisePredictiveEvent(new SetGroupLeaderMessage(ent, leader));
    }
}
