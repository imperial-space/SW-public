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


    // private readonly List<NrpMessage> _unsolvedMessages = new();
    private readonly List<CreationsPanelEui> _activeEuis = new();

    private readonly List<CreationPaintingMessage> _invokingPaintings = new();

    public List<CreationPaintingMessage> GetInvokingPaintings()
        => _invokingPaintings;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnMapInit);
        SubscribeLocalEvent<SendCreationPaintingEvent>(OnSend);
    }

    private void OnSend(SendCreationPaintingEvent args)
    {
        Logger.Debug("Send");

        var paintingMessage = new CreationPaintingMessage(args.Painting,
            args.Name,
            args.Author,
            args.SenderPlayer
        );

        Logger.Debug(AddInvokingPainting(paintingMessage) ? "Yes" : "no");

        foreach (var p in _invokingPaintings)
        {
            Logger.Debug($"Painting: {p.Painting} - {p.Name} - {p.Author} [{paintingMessage == p} {paintingMessage.Painting}]");
        }

    }


    public void RegisterEui(CreationsPanelEui eui)
    {
        Logger.Debug("Register");
        _activeEuis.Add(eui);
    }

    public void UnregisterEui(CreationsPanelEui eui)
    {
        _activeEuis.Remove(eui);
    }

    public bool AddInvokingPainting(CreationPaintingMessage painting)
    {
        if (_invokingPaintings.Contains(painting))
            return false;

        _invokingPaintings.Add(painting);

        foreach (var eui in _activeEuis)
        {
            eui.SendNewPainting(painting);
            Logger.Debug($"{eui}");
        }

        return true;
    }

    public void RemoveInvokingPainting(CreationPaintingMessage painting)
    {
        _invokingPaintings.Remove(painting);

        foreach (var eui in _activeEuis)
        {
            eui.SendRemovePainting(painting);
            Logger.Debug($"{eui}");
        }
    }


    private void OnMapInit(RoundStartAttemptEvent ev)
    {
        if (ev.Cancelled)
            return;
    }
}
