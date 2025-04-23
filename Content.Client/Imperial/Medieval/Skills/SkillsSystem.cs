using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Skills;

public sealed class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GetEnteredChatMessageMessage>(OnGetMessage);
    }

    private void OnGetMessage(GetEnteredChatMessageMessage msg)
    {
        var message = _ui.GetUIController<ChatUIController>().GetChatMessage();
        var args = new GetEnteredChatTextResponseMessage(msg.Target, msg.User, message);
        RaiseNetworkEvent(args);
    }
}
