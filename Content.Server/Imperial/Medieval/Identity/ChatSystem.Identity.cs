using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    public void SendIdentityEmote(string message, EntityUid source, ChatTransmitRange range, NetUserId? author = null)
    {
        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            EntityUid listener;

            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;
            listener = session.AttachedEntity.Value;

            var entRange = MessageRangeCheck(session, data, range);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;
            var entHideChat = entRange == MessageRangeCheckResult.HideChat;
            var wrappedMessage = Loc.GetString("chat-manager-entity-me-wrap-message",
                ("entityName", Identity.Name(source, EntityManager, listener, true)),
                ("entity", Identity.Entity(source, EntityManager)),
                ("message", FormattedMessage.RemoveMarkupOrThrow(message)));

            _chatManager.ChatMessageToOne(ChatChannel.Emotes, message, wrappedMessage, source, entHideChat, session.Channel, author: author);
        }
    }
}
