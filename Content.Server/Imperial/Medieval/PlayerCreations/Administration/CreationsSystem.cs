using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Database;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Filter = Robust.Shared.Player.Filter;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;
using Content.Shared.Imperial.Medieval.Admin;
using Robust.Server.Player;
using Content.Shared.Imperial.Medieval.Administration.Nrp;
using Robust.Shared.Network;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.MedievalPasport.Components;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Player;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.PlayerCreations.Administration;

public sealed partial class CreationsSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly EaselSystem _easel = default!;


    private readonly List<CreationsPanelEui> _activeEuis = new();

    public void RegisterEui(CreationsPanelEui eui)
    {
        _activeEuis.Add(eui);
    }

    public void UnregisterEui(CreationsPanelEui eui)
    {
        _activeEuis.Remove(eui);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SendCreationPaintingEvent>(OnSendPainting);
        SubscribeNetworkEvent<SendCreationBookEvent>(OnSendBook);
    }

    #region Paintings
    private async void OnSendPainting(SendCreationPaintingEvent args)
    {
        Logger.Debug("Send");

        if (args.Painting.Length >= 2000)
            return;
        if (args.Name.Length >= 40)
            return;
        if (args.Description.Length >= 500)
            return;

        var paintingMessage = new CreationPaintingMessage(args.Painting,
            args.Name,
            args.Description,
            args.Author,
            args.SenderPlayer,
            DateTime.UtcNow
        );

        await AddIncomingPainting(paintingMessage);

    }

    public async Task<List<Painting>> GetIncomingPaintings()
        => await _db.GetPaintings(false);

    public async Task<List<CreationPaintingMessage>> GetIncomingPaintingsMessages()
    {
        var paintings = await GetIncomingPaintings();

        var messages = paintings.Select(p =>
                new CreationPaintingMessage(PaintingHelper.StringToColors(p.Texture),
                    p.Name,
                    p.Description,
                    p.Author,
                    (NetUserId)p.AuthorUserId,
                    p.CreationTime))
            .ToList();

        return messages;
    }

    public async Task<List<Painting>> GetAcceptedPaintings()
        => await _db.GetPaintings(true);

    public async Task<List<CreationPaintingMessage>> GetAcceptedPaintingsMessages()
    {
        var paintings = await GetAcceptedPaintings();

        var messages = paintings.Select(p =>
                new CreationPaintingMessage(PaintingHelper.StringToColors(p.Texture),
                    p.Name,
                    p.Description,
                    p.Author,
                    (NetUserId)p.AuthorUserId,
                    p.CreationTime))
            .ToList();

        return messages;
    }


    public async Task<bool> AddIncomingPainting(CreationPaintingMessage painting)
    {
        var dbPainting = await _db.GetPainting(painting.Painting);

        if (dbPainting != null)
            return false;

        // _invokingPaintings.Add(painting);
        await _db.AddPainting(painting.Painting,
            painting.Name,
            painting.Description,
            painting.Author,
            painting.SenderUserId,
            painting.CreationTime,
            false
        );

        foreach (var eui in _activeEuis)
        {
            eui.SendNewIncomingPainting(painting);
            Logger.Debug($"{eui}");
        }

        return true;
    }

    public async void RemoveIncomingPainting(CreationPaintingMessage painting)
    {
        // _invokingPaintings.Remove(painting);
        Logger.Debug("removed");
        await _db.RemovePainting(painting.Painting);

        foreach (var eui in _activeEuis)
        {
            eui.SendRemoveIncomingPainting(painting);
            Logger.Debug($"{eui}");
        }
    }

    public async void AcceptIncomingPainting(CreationPaintingMessage painting)
    {
        await _db.SetPaintingAccepted(painting.Painting);
        foreach (var eui in _activeEuis)
        {
            eui.SendRemoveIncomingPainting(painting);
            eui.SendNewAcceptedPainting(painting);
        }
    }
    #endregion


    #region Books
    private async void OnSendBook(SendCreationBookEvent args)
    {
        Logger.Debug("Send book");

        if (args.Text.Length >= 12000)
            return;
        if (args.Name.Length >= 40)
            return;
        if (args.Description.Length >= 500)
            return;

        var book = new CreationBook(args.Text,
            args.Name,
            args.Description,
            args.Author,
            args.SenderPlayer,
            DateTime.UtcNow
        );

        await AddIncomingBook(book);

    }

    public async Task<List<Book>> GetIncomingBooks()
        => await _db.GetBooks(false);

    public async Task<List<CreationBook>> GetIncomingCreationBooks()
    {
        var books = await GetIncomingBooks();

        var messages = books.Select(p =>
                new CreationBook(p.Text,
                    p.Name,
                    p.Description,
                    p.Author,
                    (NetUserId)p.AuthorUserId,
                    p.CreationTime))
            .ToList();

        return messages;
    }

    public async Task<List<Book>> GetAcceptedBooks()
        => await _db.GetBooks(true);

    public async Task<List<CreationBook>> GetAcceptedCreationBooks()
    {
        var books = await GetAcceptedBooks();

        var messages = books.Select(p =>
                new CreationBook(p.Text,
                    p.Name,
                    p.Description,
                    p.Author,
                    (NetUserId)p.AuthorUserId,
                    p.CreationTime))
            .ToList();

        return messages;
    }


    public async Task<bool> AddIncomingBook(CreationBook book)
    {
        var dbPainting = await _db.GetBook(book.Text);

        if (dbPainting != null)
            return false;

        // _invokingPaintings.Add(painting);
        await _db.AddBook(book.Text,
            book.Name,
            book.Description,
            book.Author,
            book.SenderUserId,
            book.CreationTime,
            false
        );

        foreach (var eui in _activeEuis)
        {
            eui.SendNewIncomingBook(book);
            Logger.Debug($"{eui}");
        }

        return true;
    }

    public async void RemoveIncomingBook(CreationBook book)
    {
        await _db.RemoveBook(book.Text);

        foreach (var eui in _activeEuis)
        {
            eui.SendRemoveIncomingBook(book);
        }
    }

    public async void AcceptIncomingBook(CreationBook book)
    {
        await _db.SetBookAccepted(book.Text);
        foreach (var eui in _activeEuis)
        {
            eui.SendRemoveIncomingBook(book);
            eui.SendNewAcceptedBook(book);
        }
    }
    #endregion

}
