using System;
using System.Collections.Generic;
using Content.Server.Imperial.DayTime;
using Content.Server.Imperial.Medieval.Factions;
using Content.Shared.ActionBlocker;
using Content.Shared.Imperial.Medieval.Calendar;
using Content.Shared.Imperial.Medieval.Factions;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.Medieval.Calendar.Board;

public sealed class CalendarBoardSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly CalendarSystem _calendar = default!;
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public readonly List<AnnouncementData> Announcements = new();
    private const int MaxAnnouncements = 3;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CalendarBoardComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);

        SubscribeLocalEvent<CalendarBoardComponent, CalendarBoardCreateAnnouncementMessage>(OnCreateAnnouncement);
        SubscribeLocalEvent<CalendarBoardComponent, CalendarBoardDeleteAnnouncementMessage>(OnDeleteAnnouncement);
    }

    private void OnCreateAnnouncement(EntityUid uid, CalendarBoardComponent component, CalendarBoardCreateAnnouncementMessage args)
    {
        if (!_actionBlocker.CanInteract(args.Actor, uid))
            return;

        if (Announcements.Count >= MaxAnnouncements)
            return;

        var actor = args.Actor;

        if (string.IsNullOrWhiteSpace(args.Title) || string.IsNullOrWhiteSpace(args.Text))
            return;

        var title = args.Title.Trim();
        var text = args.Text.Trim();
        var authorName = string.IsNullOrWhiteSpace(args.Author)
            ? Loc.GetString("calendar-board-announcement-unknown")
            : args.Author.Trim();

        if (title.Length > 32) title = title[..32];
        if (text.Length > 256) text = text[..256];
        if (authorName.Length > 32) authorName = authorName[..32];

        var newAnnouncement = new AnnouncementData
        {
            Id = Guid.NewGuid(),
            Title = title,
            Author = authorName,
            AuthorId = GetNetEntity(actor),
            Text = text,
            CycleTime = Loc.GetString("calendar-board-day", ("day", _calendar.CurrentCycle + 1))
        };

        Announcements.Add(newAnnouncement);
        UpdateAllBoards();
    }

    private void OnDeleteAnnouncement(EntityUid uid, CalendarBoardComponent component, CalendarBoardDeleteAnnouncementMessage args)
    {
        if (!_actionBlocker.CanInteract(args.Actor, uid))
            return;

        var actorNetEntity = GetNetEntity(args.Actor);

        Announcements.RemoveAll(a => a.Id == args.Id && a.AuthorId == actorNetEntity);
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
            stringDayDeck.Add((string) protoId);
        }

        var stringNightDeck = new List<string>(nightDeck.Count);
        foreach (var protoId in nightDeck)
        {
            stringNightDeck.Add((string) protoId);
        }

        var wantedData = new Dictionary<int, WantedData>(_factions.WantedList);

        var state = new CalendarBoardBoundUserInterfaceState(wantedData, stringDayDeck, stringNightDeck, currentCycle, Announcements);
        _ui.SetUiState(uid, CalendarBoardUiKey.Key, state);

        _appearance.SetData(uid, WantedDeskVisuals.Appearance, wantedData.Count switch
        {
            <= 0 => WantedDeskVisualState.None,
            < 3 => WantedDeskVisualState.Min,
            < 6 => WantedDeskVisualState.Medium,
            > 6 => WantedDeskVisualState.Full,
            _ => WantedDeskVisualState.None
        });
    }
}
