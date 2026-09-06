using Content.Client.Imperial.Medieval.Skills.UI;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Imperial.Medieval.GhostSkills;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Skills;

public sealed class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private GhostSkillsMenu? _ghostSkillsMenu;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GetEnteredChatMessageMessage>(OnGetMessage);
        SubscribeNetworkEvent<OpenAdminSkillsMenuMessage>(OnAdminSkills);
        SubscribeNetworkEvent<OpenGhostSkillsMenuMessage>(OnOpenGhostSkills);
        SubscribeNetworkEvent<GhostSkillsSavedMessage>(OnGhostSkillsSaved);
    }

    private void OnGetMessage(GetEnteredChatMessageMessage msg)
    {
        var message = _ui.GetUIController<ChatUIController>().GetChatMessage();
        var args = new GetEnteredChatTextResponseMessage(msg.Target, msg.User, message);
        RaiseNetworkEvent(args);
    }

    private void OnAdminSkills(OpenAdminSkillsMenuMessage msg)
    {
        var controller = _ui.GetUIController<AdminSkillsMenuUiController>();
        controller.OpenMenu(msg.Target, msg.Levels);
    }

    private void OnOpenGhostSkills(OpenGhostSkillsMenuMessage message)
    {
        _ghostSkillsMenu?.Close();
        _ghostSkillsMenu = new GhostSkillsMenu(_prototypes, message.Levels);
        _ghostSkillsMenu.SavePressed += levels => RaiseNetworkEvent(new SaveGhostSkillsMessage(levels));
        _ghostSkillsMenu.OnClose += () => _ghostSkillsMenu = null;
        _ghostSkillsMenu.OpenCentered();
    }

    private void OnGhostSkillsSaved(GhostSkillsSavedMessage message)
    {
        _ghostSkillsMenu?.Close();
    }
}
