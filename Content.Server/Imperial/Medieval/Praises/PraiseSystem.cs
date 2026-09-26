using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.ICCVar;
using Content.Shared.Imperial.Medieval.Praises;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Server.Administration.Managers;

namespace Content.Server.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminMan = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IConfigurationManager _cfgMan = default!;
    [Dependency] private readonly IServerDbManager _dbMan = default!;
    [Dependency] private readonly UserDbDataManager _userDbDataMan = default!;

    private Dictionary<NetUserId, int> _remainingPraises = new();
    private Dictionary<NetUserId, List<Praise>> _praises = new(); //recent praises sent to each player before the current round (to check if he was praised recently)
    private Dictionary<NetUserId, List<Praise>> _newPraises = new(); //praises sent to each player during this round (to be written later)
    private Dictionary<NetUserId, int> _praiseRating = new(); //total praise weight of each player
    private Dictionary<NetUserId, ICommonSession> _lastPraiseTarget = new(); //used by praise window
    private Dictionary<NetUserId, DateTime> _lastPraiseViewDataRequests = new(); //time of the last praise view data request made by this player (to prevent spam)

    private readonly TimeSpan _praiseViewDataRequestCooldown = TimeSpan.FromSeconds(5); //to prevent spam

    //don't forget to change it in 'PraiseWindow' and 'Praise' class too
    //(I didn't want to create a separate static class for storing a single constant and putting it anywhere else would be strange)
    //(also couldn't import the praise namespace into DB model for some reason)
    private const int MaxPraiseReasonLength = 50;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PraiseComponent, PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<PraiseComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeNetworkEvent<AddPraiseMessage>(OnAddPraise);
        SubscribeNetworkEvent<PraiseWindowPraiseMessage>(OnPraiseWindowSend);
        SubscribeNetworkEvent<PraiseRatingOpenedMessage>(OnPraiseRatingOpened);
        SubscribeNetworkEvent<PraiseViewOpenedMessage>(OnPraiseViewOpened);
        SubscribeNetworkEvent<PraiseViewDeleteMessage>(OnPraiseViewDelete);
        SubscribeNetworkEvent<PraiseViewEditMessage>(OnPraiseViewEdit);
    }

    private void OnSpawnComplete(EntityUid uid, PraiseComponent praise, PlayerSpawnCompleteEvent args)
    {
        AddPlayerData(args.Player); //since only async methods can use 'await' I had to do this
    }

    private async void AddPlayerData(ICommonSession player)
    {
        List<Praise> praises = await _dbMan.GetPraises(player.UserId);

        _praiseRating[player.UserId] = 0;
        foreach (Praise praise in praises)
        {
            _praiseRating[player.UserId] += praise.Weight;
        }

        //collecting only the recent entries to speed up search, would do this in DB manager but that would require getting access to config manager from there to get the CD CVar value
        DateTime tp = DateTime.Now - _cfgMan.GetCVar(ICCVars.PraiseCooldown);
        _praises[player.UserId] = praises.Where(p => p.Date > tp).ToList();
    }

    private bool CanPraise(ICommonSession user, ICommonSession target, out bool noPraises, out bool praisedRecently)
    {
        noPraises = praisedRecently = false;

        if (!_remainingPraises.ContainsKey(user.UserId))
            _remainingPraises[user.UserId] = _cfgMan.GetCVar(ICCVars.PraisesPerRound);

        if (!_praises.ContainsKey(target.UserId))
            _praises[target.UserId] = new(); //shouldn't happen but just in case

        if (!_newPraises.ContainsKey(target.UserId))
            _newPraises[target.UserId] = new();

        if (_adminMan.IsAdmin(user))
            return true;

        if (_remainingPraises[user.UserId] <= 0)
            noPraises = true;

        IEnumerable<Praise> praises = _praises[target.UserId].Concat(_newPraises[target.UserId]);
        DateTime tp = DateTime.Now - _cfgMan.GetCVar(ICCVars.PraiseCooldown);
        foreach (Praise praise in praises)
        {
            if (praise.GivenBy == user.UserId && praise.Date > tp)
                praisedRecently = true;
        }

        return !(noPraises || praisedRecently);
    }

    private PraiseWindowMessage GenerateMessage(ICommonSession user, ICommonSession target)
    {
        bool canPraise = CanPraise(user, target, out bool noPraises, out bool praisedRecently);

        string msg = Loc.GetString("praises-window-info", ("count", _remainingPraises[user.UserId]));
        if (noPraises)
            msg = Loc.GetString("praises-window-outofpraises");
        if (praisedRecently)
            msg = Loc.GetString("praises-window-praisedrecently");

        return new PraiseWindowMessage { Message = msg, SendButtonDisabled = !canPraise };
    }

    private void OnGetVerbs(EntityUid uid, PraiseComponent praise, ref GetVerbsEvent<ExamineVerb> args)
    {
        EntityUid userUid = args.User;
        if (!TryComp<PraiseComponent>(userUid, out var praiseUser) ||
            !_playerMan.TryGetSessionByEntity(uid, out var target) ||
            uid == userUid)
            return;

        args.Verbs.Add(new ExamineVerb()
        {
            Act = () =>
            {
                if (!_playerMan.TryGetSessionByEntity(uid, out var target) ||
                    !_playerMan.TryGetSessionByEntity(userUid, out var user))
                    return;

                _lastPraiseTarget[user.UserId] = target;
                RaiseNetworkEvent(GenerateMessage(user, target), user);
            },
            CloseMenu = true,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Medieval/date.rsi"), "date"),
            Text = Loc.GetString("praises-verbname")
        });
    }

    private void OnAddPraise(AddPraiseMessage ev, EntitySessionEventArgs args)
    {
        ICommonSession user = args.SenderSession;

        if (!_adminMan.IsAdmin(user))
            return;

        if (!_newPraises.ContainsKey(ev.Target))
            _newPraises[ev.Target] = new();

        _newPraises[ev.Target].Add(new Praise
        {
            GivenTo = ev.Target,
            GivenBy = user.UserId,
            Date = DateTime.Now,
            GivenByName = user.Name,
            Reason = ev.Reason,
            Weight = ev.Weight
        });
    }

    private void OnPraiseWindowSend(PraiseWindowPraiseMessage msg, EntitySessionEventArgs args)
    {
        ICommonSession user = args.SenderSession;

        if (msg.Reason.Length > MaxPraiseReasonLength ||
            !_lastPraiseTarget.TryGetValue(user.UserId, out var target) ||
            !TryComp<PraiseComponent>(user.AttachedEntity, out var praise))
            return;

        if (CanPraise(user, target, out _, out _))
        {
            _remainingPraises[user.UserId] -= 1;
            _newPraises[target.UserId].Add(new Praise
            {
                GivenTo = target.UserId,
                GivenBy = user.UserId,
                Date = DateTime.Now,
                GivenByName = user.Name,
                Reason = msg.Reason,
                Weight = praise.Weight
            });
        }

        RaiseNetworkEvent(GenerateMessage(user, target), user);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var values in _newPraises.Values)
        {
            _dbMan.AddPraises(values);
        }

        _remainingPraises.Clear();
        _praises.Clear();
        _newPraises.Clear();
        _lastPraiseViewDataRequests.Clear();
    }

    private void OnPraiseRatingOpened(PraiseRatingOpenedMessage ev, EntitySessionEventArgs args)
    {
        Log.Debug("rating: Received 'PraiseRatingOpenedMessage' from client.");
        if (!_adminMan.IsAdmin(args.SenderSession))
        {
            Log.Debug($"rating: '{args.SenderSession.Name}' is not an admin, request refused.");
            return;
        }

        Log.Debug($"rating: Creating a rating; Total of {_praiseRating.Count} players registered");
        PraiseRatingMessage msg = new();
        msg.Rating = new();
        foreach ((NetUserId id, int weight) in _praiseRating)
        {
            if (!_playerMan.TryGetSessionById(id, out var player))
            {
                Log.Debug($"rating: Player {id} disconnected, not adding to rating.");
                return;
            }

            msg.Rating.Add((player.Name, weight));
        }

        Log.Debug("rating: Sending 'PraiseRatingMessage' to client.");
        RaiseNetworkEvent(msg, args.SenderSession);
    }

    private async void OnPraiseViewOpened(PraiseViewOpenedMessage msg, EntitySessionEventArgs args)
    {
        NetUserId id = args.SenderSession.UserId;

        bool isAdmin = _adminMan.IsAdmin(args.SenderSession);
        if (msg.Target != id && !isAdmin)
            return;

        if (!_lastPraiseViewDataRequests.ContainsKey(id))
            _lastPraiseViewDataRequests[id] = DateTime.MinValue;

        if (!_newPraises.ContainsKey(msg.Target))
            _newPraises[msg.Target] = new();

        if (!isAdmin && _lastPraiseViewDataRequests[id] > DateTime.Now - _praiseViewDataRequestCooldown)
        {
            RaiseNetworkEvent(new PraiseViewMessage { Target = msg.Target, Records = new(), Admin = false, Spam = true }, args.SenderSession);
            return;
        }

        _lastPraiseViewDataRequests[id] = DateTime.Now;
        List<PraiseViewRecord> records = new();
        IEnumerable<Praise> praises = (await _dbMan.GetPraises(msg.Target)).Concat(_newPraises[msg.Target]);
        foreach (Praise praise in praises)
        {
            records.Add(new PraiseViewRecord
            {
                GivenBy = isAdmin ? praise.GivenBy : null,
                GivenByName = praise.GivenByName,
                Reason = praise.Reason,
                Date = praise.Date,
                Weight = praise.Weight
            });
        }

        RaiseNetworkEvent(new PraiseViewMessage { Target = msg.Target, Records = records, Admin = isAdmin, Spam = false }, args.SenderSession);
    }

    private void OnPraiseViewEdit(PraiseViewEditMessage ev, EntitySessionEventArgs args)
    {
        if (!_adminMan.IsAdmin(args.SenderSession) || ev.Record.GivenBy == null)
            return;

        List<Praise> praises = _newPraises[ev.Target];
        foreach (Praise praise in praises)
        {
            if (praise.GivenBy == ev.Record.GivenBy.Value && praise.GivenTo == ev.Target && praise.Date == ev.Record.Date)
            {
                praise.GivenByName = ev.Record.GivenByName;
                praise.Reason = ev.Record.Reason;
                praise.Weight = ev.Record.Weight;
                return;
            }
        }

        Praise newPraise = new Praise
        {
            GivenTo = ev.Target,
            GivenBy = ev.Record.GivenBy.Value,
            Date = ev.Record.Date,
            GivenByName = ev.Record.GivenByName,
            Reason = ev.Record.Reason,
            Weight = ev.Record.Weight
        };

        _dbMan.EditPraise(newPraise);
    }

    private void OnPraiseViewDelete(PraiseViewDeleteMessage ev, EntitySessionEventArgs args)
    {
        if (!_adminMan.IsAdmin(args.SenderSession) || ev.Record.GivenBy == null)
            return;

        List<Praise> praises = _newPraises[ev.Target];
        for (int i = 0; i < praises.Count; i++)
        {
            Praise praise = praises[i];
            if (praise.GivenBy == ev.Record.GivenBy.Value && praise.GivenTo == ev.Target && praise.Date == ev.Record.Date)
            {
                praises.RemoveAt(i);
                return;
            }
        }

        _dbMan.RemovePraise(ev.Target, ev.Record.GivenBy.Value, ev.Record.Date);
    }
}
