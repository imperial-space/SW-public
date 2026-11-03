using System;
using System.Collections.Generic;
using Content.Server.Administration.Managers;
using Content.Server.Imperial.DayTime;
using Content.Server.Imperial.Medieval.Factions;
using Content.Shared.ActionBlocker;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Calendar;
using Content.Shared.Imperial.Medieval.Factions;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Calendar.Board;

public sealed class CalendarBoardSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly CalendarSystem _calendar = default!;
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    public readonly List<AnnouncementData> Announcements = new();
    private const int MaxAnnouncementsPerPlayer = 3;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<CalendarBoardComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);
        SubscribeLocalEvent<CalendarEventStartedEvent>(OnCalendarEventStarted);

        SubscribeLocalEvent<CalendarBoardComponent, CalendarBoardCreateAnnouncementMessage>(OnCreateAnnouncement);
        SubscribeLocalEvent<CalendarBoardComponent, CalendarBoardDeleteAnnouncementMessage>(OnDeleteAnnouncement);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        Announcements.Clear();
    }

    private void OnCalendarEventStarted(CalendarEventStartedEvent args)
    {
        UpdateAllBoards();
    }

    private void OnCreateAnnouncement(EntityUid uid, CalendarBoardComponent component, CalendarBoardCreateAnnouncementMessage args)
    {
        if (!_actionBlocker.CanInteract(args.Actor, uid))
            return;

        var actor = args.Actor;

        NetUserId? authorUserId = null;
        if (TryComp<ActorComponent>(actor, out var actorComp))
        {
            authorUserId = actorComp.PlayerSession.UserId;
        }

        var actorNetEntity = GetNetEntity(actor);

        var playerAnnouncementsCount = 0;
        foreach (var ann in Announcements)
        {
            if ((authorUserId != null && ann.AuthorUserId == authorUserId) ||
                (ann.AuthorId != null && ann.AuthorId == actorNetEntity))
            {
                playerAnnouncementsCount++;
            }
        }

        if (playerAnnouncementsCount >= MaxAnnouncementsPerPlayer)
            return;

        if (string.IsNullOrWhiteSpace(args.Title) || string.IsNullOrWhiteSpace(args.Text))
            return;

        var title = args.Title.Trim();
        var text = args.Text.Trim();
        var authorName = string.IsNullOrWhiteSpace(args.Author)
            ? Loc.GetString("calendar-board-announcement-unknown")
            : args.Author.Trim();

        if (title.Length > 32)
            title = title[..32];
        if (text.Length > 256)
            text = text[..256];
        if (authorName.Length > 32)
            authorName = authorName[..32];

        string? adminInfo = null;
        if (actorComp != null)
        {
            var ckey = actorComp.PlayerSession.Name;
            var characterName = MetaData(actor).EntityName;
            adminInfo = Loc.GetString("calendar-board-announcement-admin-info",
                ("ckey", ckey),
                ("character", characterName));
        }

        var newAnnouncement = new AnnouncementData
        {
            Id = Guid.NewGuid(),
            Title = title,
            Author = authorName,
            AuthorId = actorNetEntity,
            AuthorUserId = authorUserId,
            Text = text,
            CycleTime = Loc.GetString("calendar-board-day", ("day", _calendar.CurrentCycle + 1)),
            AdminInfo = adminInfo
        };

        Announcements.Add(newAnnouncement);
        UpdateAllBoards();
    }

    private void OnDeleteAnnouncement(EntityUid uid, CalendarBoardComponent component, CalendarBoardDeleteAnnouncementMessage args)
    {
        if (!_actionBlocker.CanInteract(args.Actor, uid))
            return;

        var actorNetEntity = GetNetEntity(args.Actor);
        NetUserId? userId = TryComp<ActorComponent>(args.Actor, out var actorComp)
            ? actorComp.PlayerSession.UserId
            : null;

        var isAdmin = actorComp != null && _adminManager.IsAdmin(actorComp.PlayerSession);

        Announcements.RemoveAll(a => a.Id == args.Id && (isAdmin || a.AuthorId == actorNetEntity || (userId != null && a.AuthorUserId == userId)));
        UpdateAllBoards();
    }

    private void OnUIOpened(EntityUid uid, CalendarBoardComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not CalendarBoardUiKey.Key)
            return;

        UpdateUIState(uid);
    }

    private void OnDayCycleFinished(ref DayCycleFinishedEvent args)
    {
        UpdateAllBoards();
    }

    public void UpdateAllBoards()
    {
        var query = EntityQueryEnumerator<CalendarBoardComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            UpdateUIState(uid);
        }
    }

    public void UpdateUIState(EntityUid uid)
    {
        var dayDeck = _calendar.DayDeck;
        var nightDeck = _calendar.NightDeck;
        var currentCycle = _calendar.CurrentCycle;

        var stringDayDeck = new List<string>(dayDeck.Count);
        foreach (var protoId in dayDeck)
        {
            stringDayDeck.Add(protoId.Id);
        }

        var stringNightDeck = new List<string>(nightDeck.Count);
        foreach (var protoId in nightDeck)
        {
            stringNightDeck.Add(protoId.Id);
        }

        var wantedData = new Dictionary<int, WantedData>(_factions.WantedList);

        var state = new CalendarBoardBoundUserInterfaceState(wantedData, stringDayDeck, stringNightDeck, currentCycle, Announcements);
        _ui.SetUiState(uid, CalendarBoardUiKey.Key, state);

        int papersCount = Announcements.Count + wantedData.Count;

        _appearance.SetData(uid, WantedDeskVisuals.Appearance, papersCount switch
        {
            <= 0 => WantedDeskVisualState.None,
            < 3 => WantedDeskVisualState.Min,
            < 6 => WantedDeskVisualState.Medium,
            >= 6 => WantedDeskVisualState.Full
        });
    }
}
