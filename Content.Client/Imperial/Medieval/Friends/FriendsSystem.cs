using Content.Client.Imperial.Medieval.Friends.UI;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Friends;

public sealed partial class FriendsSystem : SharedFriendsSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public static Dictionary<FactionMemberGroup, (Color, string)> FactionGroups = new()
    {
        { FactionMemberGroup.None, (Color.FromHex("#1f1f24"), "Назначить группу") },
        { FactionMemberGroup.Alpha, (Color.FromHex("#794646"), "Альфа") },
        { FactionMemberGroup.Bravo, (Color.FromHex("#4E4679"), "Браво") },
        { FactionMemberGroup.Delta, (Color.FromHex("#467953"), "Дельта") },
        { FactionMemberGroup.Gamma, (Color.FromHex("#797746"), "Браво") },
        { FactionMemberGroup.Omega, (Color.FromHex("#6F4679"), "Омега") },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionDataContainerComponent, AfterAutoHandleStateEvent>(AfterAutoHandleState);
    }

    private void AfterAutoHandleState(EntityUid uid, FactionDataContainerComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_player.LocalEntity.HasValue)
            return;
        var player = _player.LocalEntity.Value;
        if (!TryComp<FriendsComponent>(player, out var friends))
            return;
        if (TryGetFactionDataContainer(out var container) && container.Value.Comp.CachedMembers.TryGetValue(friends.Faction, out var val))
            _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(friends.Faction, val);
    }

    public override void OpenMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data)
    {
        if (_time.IsFirstTimePredicted)
        {
            _uiMan.GetUIController<FactionMenuUiController>().ToggleMenu();
            if (TryGetFactionDataContainer(out var container) && container.Value.Comp.CachedMembers.TryGetValue(proto, out var val))
                _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(proto, val);
        }
    }
}
