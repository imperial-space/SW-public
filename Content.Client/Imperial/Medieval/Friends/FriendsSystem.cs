using Content.Client.Imperial.Medieval.Friends.UI;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Friends;

public sealed partial class FriendsSystem : SharedFriendsSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IGameTiming _time = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionHeadComponent, AfterAutoHandleStateEvent>(AfterAutoHandleState);
    }

    private void AfterAutoHandleState(EntityUid uid, FactionHeadComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (_time.IsFirstTimePredicted)
            _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(comp.CachedMembers);
    }

    public override void OpenMenu(Dictionary<NetEntity, FactionMemberData> data)
    {
        if (_time.IsFirstTimePredicted)
            _uiMan.GetUIController<FactionMenuUiController>().ToggleMenu(data);
    }
}
