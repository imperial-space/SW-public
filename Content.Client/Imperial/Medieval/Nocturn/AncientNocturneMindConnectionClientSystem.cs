using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Content.Shared.Nocturn.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Nocturn;

public sealed class AncientNocturneMindConnectionClientSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<ChatBox, ChatBoxHandlers> _chatBoxHandlers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AncientNocturneConversionNotificationEvent>(OnConversionNotification);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var chatController = _ui.GetUIController<ChatUIController>();
        foreach (var chatBox in chatController.Chats)
        {
            if (!_chatBoxHandlers.ContainsKey(chatBox))
                RegisterChatBox(chatBox);

            UpdateSelectedChannel(chatBox);
        }

        foreach (var chatBox in _chatBoxHandlers.Keys
                     .Where(chatBox => !chatController.Chats.Contains(chatBox))
                     .ToList())
        {
            UnregisterChatBox(chatBox);
        }
    }

    public override void Shutdown()
    {
        foreach (var chatBox in _chatBoxHandlers.Keys.ToList())
        {
            UnregisterChatBox(chatBox);
        }

        base.Shutdown();
    }

    private void OnConversionNotification(AncientNocturneConversionNotificationEvent args)
    {
        var messageId = args.Type switch
        {
            AncientNocturneConversionNotification.FirstTrall =>
                "medieval-ancient-nocturne-mind-connection-first-trall",
            AncientNocturneConversionNotification.Converted =>
                "medieval-ancient-nocturne-mind-connection-converted",
            _ => throw new ArgumentOutOfRangeException()
        };

        var key = Loc.GetString("medieval-ancient-nocturne-mind-connection-chat-key");
        var message = Loc.GetString(messageId, ("key", key));
        var chatMessage = new ChatMessage(
            ChatChannel.Server,
            message,
            FormattedMessage.EscapeText(message),
            default,
            null,
            colorOverride: Color.Yellow);

        _ui.GetUIController<ChatUIController>().ProcessChatMessage(chatMessage, false);
    }

    private void RegisterChatBox(ChatBox chatBox)
    {
        Action<GUIBoundKeyEventArgs> submit = args => OnChatKeyBindDown(chatBox, args);
        Action<LineEdit.LineEditEventArgs> textChanged = _ => UpdateSelectedChannel(chatBox);
        _chatBoxHandlers.Add(chatBox, new ChatBoxHandlers(submit, textChanged));
        chatBox.ChatInput.Input.OnKeyBindDown += submit;
        chatBox.ChatInput.Input.OnTextChanged += textChanged;
    }

    private void UnregisterChatBox(ChatBox chatBox)
    {
        if (!_chatBoxHandlers.Remove(chatBox, out var handlers))
            return;

        chatBox.ChatInput.Input.OnKeyBindDown -= handlers.Submit;
        chatBox.ChatInput.Input.OnTextChanged -= handlers.TextChanged;
    }

    private void OnChatKeyBindDown(ChatBox chatBox, GUIBoundKeyEventArgs args)
    {
        var input = chatBox.ChatInput.Input.Text;
        if (args.Function != EngineKeyFunctions.TextSubmit ||
            !TryGetMindChat(out var chat) ||
            !TryGetMindMessage(input, chat, out var message))
            return;

        chatBox.ChatInput.Input.Clear();
        chatBox.ChatInput.Input.ReleaseKeyboardFocus();
        args.Handle();

        if (string.IsNullOrWhiteSpace(message))
            return;

        var chatController = _ui.GetUIController<ChatUIController>();
        input = input.Trim();
        if (input.Length > chatController.MaxMessageLength)
        {
            var warning = Loc.GetString(
                "chat-manager-max-message-length",
                ("maxMessageLength", chatController.MaxMessageLength));
            chatBox.AddLine(warning, Color.Orange);
            return;
        }

        RaiseNetworkEvent(new AncientNocturneMindChatMessageEvent(input));
    }

    private void UpdateSelectedChannel(ChatBox chatBox)
    {
        if (!TryGetMindChat(out var chat) ||
            !TryGetMindMessage(chatBox.ChatInput.Input.Text, chat, out _))
            return;

        chatBox.ChatInput.ChannelSelector.Text =
            Loc.GetString("medieval-ancient-nocturne-mind-connection-channel-name");
        chatBox.ChatInput.ChannelSelector.Modulate = chat.ChatColor;
    }

    private bool TryGetMindChat(out AncientNocturneMindChatComponent chat)
    {
        chat = default!;
        if (_player.LocalEntity is not { Valid: true } entity ||
            !TryComp<AncientNocturneMindChatComponent>(entity, out var component))
            return false;

        chat = component;
        return true;
    }

    private static bool TryGetMindMessage(
        string text,
        AncientNocturneMindChatComponent chat,
        out string message)
    {
        text = text.TrimStart();
        var prefix = text.StartsWith(chat.ChatPrefix, StringComparison.OrdinalIgnoreCase)
            ? chat.ChatPrefix
            : text.StartsWith(chat.AlternateChatPrefix, StringComparison.OrdinalIgnoreCase)
                ? chat.AlternateChatPrefix
                : null;

        if (prefix == null)
        {
            message = string.Empty;
            return false;
        }

        message = text[prefix.Length..].TrimStart();
        return true;
    }

    private sealed record ChatBoxHandlers(
        Action<GUIBoundKeyEventArgs> Submit,
        Action<LineEdit.LineEditEventArgs> TextChanged);
}
