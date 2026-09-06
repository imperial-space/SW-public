using System.Globalization;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Nocturn.Components;
using Content.Shared.Players;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Polymorph;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Nocturn;

public sealed class AncientNocturneMindConnectionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AncientNocturneMindConnectionComponent, ComponentStartup>(OnMasterStartup);
        SubscribeLocalEvent<AncientNocturneMindConnectionComponent, ComponentShutdown>(OnMasterShutdown);
        SubscribeLocalEvent<AncientNocturneTrallMindConnectionComponent, ComponentShutdown>(OnTrallShutdown);
        SubscribeLocalEvent<AncientNocturneMindConnectionComponent, PolymorphedEvent>(OnMasterPolymorphed);
        SubscribeLocalEvent<AncientNocturneTrallMindConnectionComponent, PolymorphedEvent>(OnTrallPolymorphed);
        SubscribeNetworkEvent<AncientNocturneMindChatMessageEvent>(OnMindMessage);
    }

    private void OnMasterStartup(
        Entity<AncientNocturneMindConnectionComponent> ent,
        ref ComponentStartup args)
    {
        ent.Comp.ActiveEntity = ent.Owner;
        EnsureComp<AncientNocturneMindChatComponent>(ent.Owner);
    }

    private void OnMasterShutdown(
        Entity<AncientNocturneMindConnectionComponent> ent,
        ref ComponentShutdown args)
    {
        var chatColor = TryComp<AncientNocturneMindChatComponent>(ent.Owner, out var chat)
            ? chat.ChatColor
            : Color.FromHex("#A060E8");

        foreach (var trallUid in ent.Comp.Tralls.ToArray())
        {
            if (!TryComp<AncientNocturneTrallMindConnectionComponent>(trallUid, out var trall) ||
                trall.Master != ent.Owner)
                continue;

            SendConnectionSevered(trallUid, chatColor);
            RemComp<AncientNocturneTrallMindConnectionComponent>(trallUid);
        }

        if (ent.Comp.ActiveEntity is { } active &&
            active != ent.Owner &&
            TryComp<AncientNocturneTrallMindConnectionComponent>(active, out var relay) &&
            relay.IsMasterRelay &&
            relay.Master == ent.Owner)
        {
            RemComp<AncientNocturneTrallMindConnectionComponent>(active);
        }

        ent.Comp.Tralls.Clear();
        RemComp<AncientNocturneMindChatComponent>(ent.Owner);
    }

    private void OnTrallShutdown(
        Entity<AncientNocturneTrallMindConnectionComponent> ent,
        ref ComponentShutdown args)
    {
        RemComp<AncientNocturneMindChatComponent>(ent.Owner);

        if (ent.Comp.IsMasterRelay)
            return;

        if (TryComp<AncientNocturneMindConnectionComponent>(ent.Comp.Master, out var master))
            master.Tralls.Remove(ent.Owner);
    }

    private void OnMindMessage(
        AncientNocturneMindChatMessageEvent args,
        EntitySessionEventArgs eventArgs)
    {
        var player = eventArgs.SenderSession;
        if (player.AttachedEntity is not { Valid: true } source ||
            player.ContentData()?.Mind == null ||
            !TryComp<AncientNocturneMindChatComponent>(source, out var chat))
            return;

        if (_chat.HandleRateLimit(player) != RateLimitStatus.Allowed ||
            _chat.MessageCharacterLimit(player, args.Message))
            return;

        var culture = CultureInfo.CurrentCulture;
        var capitalizeTheWordI = (!culture.IsNeutralCulture && culture.Parent.Name == "en") ||
                                 (culture.IsNeutralCulture && culture.Name == "en");
        var message = _chatSystem.SanitizeInGameICMessage(
            source,
            args.Message,
            out _,
            punctuate: _configuration.GetCVar(CCVars.ChatPunctuation),
            capitalizeTheWordI: capitalizeTheWordI);
        var prefix = message.StartsWith(chat.ChatPrefix, StringComparison.OrdinalIgnoreCase)
            ? chat.ChatPrefix
            : message.StartsWith(chat.AlternateChatPrefix, StringComparison.OrdinalIgnoreCase)
                ? chat.AlternateChatPrefix
                : null;
        if (prefix == null)
            return;

        message = message[prefix.Length..].TrimStart();
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (TryComp<AncientNocturneMindConnectionComponent>(source, out var master))
        {
            SendMindMessage((source, master), source, source, message, chat);
            return;
        }

        if (!TryComp<AncientNocturneTrallMindConnectionComponent>(source, out var trall) ||
            !TryComp<AncientNocturneMindConnectionComponent>(trall.Master, out master))
            return;

        var nameSource = trall.IsMasterRelay ? trall.Master : source;
        SendMindMessage((trall.Master, master), source, nameSource, message, chat);
    }

    private void SendMindMessage(
        Entity<AncientNocturneMindConnectionComponent> master,
        EntityUid source,
        EntityUid nameSource,
        string message,
        AncientNocturneMindChatComponent chat)
    {
        var recipients = Filter.Empty();
        if (master.Comp.ActiveEntity is { } activeMaster)
            AddRecipient(recipients, activeMaster);
        else
            AddRecipient(recipients, master.Owner);

        foreach (var trallUid in master.Comp.Tralls.ToArray())
        {
            if (TerminatingOrDeleted(trallUid) ||
                !TryComp<AncientNocturneTrallMindConnectionComponent>(trallUid, out var trall) ||
                trall.Master != master.Owner)
            {
                master.Comp.Tralls.Remove(trallUid);
                continue;
            }

            AddRecipient(recipients, trallUid);
        }

        var escapedName = FormattedMessage.EscapeText(Name(nameSource));
        var escapedMessage = FormattedMessage.EscapeText(message);
        var channel = Loc.GetString("medieval-ancient-nocturne-mind-connection-channel-name");
        var wrappedMessage = Loc.GetString(
            "medieval-ancient-nocturne-mind-connection-wrap-message",
            ("channel", $"\\[{channel}\\]"),
            ("name", escapedName),
            ("message", escapedMessage));
        wrappedMessage = $"[color={chat.ChatColor.ToHex()}]{wrappedMessage}[/color]";

        _chat.ChatMessageToManyFiltered(
            recipients,
            ChatChannel.Radio,
            message,
            wrappedMessage,
            source,
            false,
            false,
            chat.ChatColor);
    }

    private void OnMasterPolymorphed(
        Entity<AncientNocturneMindConnectionComponent> ent,
        ref PolymorphedEvent args)
    {
        if (args.IsRevert)
            return;

        ent.Comp.ActiveEntity = args.NewEntity;
        var relay = EnsureComp<AncientNocturneTrallMindConnectionComponent>(args.NewEntity);
        relay.Master = ent.Owner;
        relay.IsMasterRelay = true;
        EnsureComp<AncientNocturneMindChatComponent>(args.NewEntity);
    }

    private void OnTrallPolymorphed(
        Entity<AncientNocturneTrallMindConnectionComponent> ent,
        ref PolymorphedEvent args)
    {
        if (!ent.Comp.IsMasterRelay ||
            !TryComp<AncientNocturneMindConnectionComponent>(ent.Comp.Master, out var master))
            return;

        master.ActiveEntity = args.NewEntity;
        if (args.IsRevert)
            return;

        var relay = EnsureComp<AncientNocturneTrallMindConnectionComponent>(args.NewEntity);
        relay.Master = ent.Comp.Master;
        relay.IsMasterRelay = true;
        EnsureComp<AncientNocturneMindChatComponent>(args.NewEntity);
    }

    private void SendConnectionSevered(EntityUid trall, Color color)
    {
        if (!TryComp<ActorComponent>(trall, out var actor))
            return;

        var message = Loc.GetString("medieval-ancient-nocturne-mind-connection-severed");
        var wrappedMessage = $"[color={color.ToHex()}]{FormattedMessage.EscapeText(message)}[/color]";
        _chat.ChatMessageToOne(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            EntityUid.Invalid,
            false,
            actor.PlayerSession.Channel,
            color);
    }

    private void AddRecipient(Filter filter, EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            filter.AddPlayer(actor.PlayerSession);
    }
}
