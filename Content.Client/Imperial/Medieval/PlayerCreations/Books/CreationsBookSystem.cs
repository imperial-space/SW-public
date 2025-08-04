using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Imperial.Medieval.PlayerCreations.Books;
using Content.Client.Imperial.Medieval.PlayerCreations.Paintings;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Content.Shared.Imperial.Medieval.PlayerCreations.Books;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Client.Player;
using SixLabors.ImageSharp.PixelFormats;


namespace Content.Server.Imperial.Medieval.PlayerCreations.Books;
public sealed class CreationsBookSystem : EntitySystem
{

    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreationsBookComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, CreationsBookComponent comp, GetVerbsEvent<Verb> args)
    {
        Verb send = new()
        {
            Text = Loc.GetString("creations-book-verb-send"),
            ClientExclusive = true,
            Act = () =>
            {
                if (!TryComp<PaperComponent>(uid, out var paper))
                    return;



                var bookSendWindow = new BookSendDialogWindow((name, description, author) =>
                {
                    OnSend(paper.Content, name, description, author);
                });
                bookSendWindow.Open();
            },
        };
        args.Verbs.Add(send);
    }

    private void OnSend(string content, string name, string description, string author)
    {
        if (_playerManager.LocalUser == null)
            return;
        var ev = new SendCreationBookEvent(content, name, description, author, _playerManager.LocalUser.Value);
        RaiseNetworkEvent(ev);
    }
}
