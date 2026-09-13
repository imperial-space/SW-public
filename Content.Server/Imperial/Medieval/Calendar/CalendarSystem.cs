using Content.Server.Administration;
using Content.Server.Imperial.DayTime;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;

namespace Content.Shared.Imperial.Medieval.Calendar;

public sealed class CalendarSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    public const int DayStageNumber = 12;
    public const int NightStageNumber = 6;

    public const string DayTag = "Day";
    public const string NightTag = "Night";

    public const int TargetDaysCount = 12;

    private int _curCycle;
    private readonly List<ProtoId<CalendarEventPrototype>> _dayDeck = new();
    private readonly List<ProtoId<CalendarEventPrototype>> _nightDeck = new();

    public IReadOnlyList<ProtoId<CalendarEventPrototype>> DayDeck => _dayDeck;
    public IReadOnlyList<ProtoId<CalendarEventPrototype>> NightDeck => _nightDeck;
    public int CurrentCycle => _curCycle;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DayCycleStageChangedEvent>(OnDayCycleChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<CalendarEventStartedEvent>(OnCalendarEventStarted);
    }

    private void OnCalendarEventStarted(CalendarEventStartedEvent args)
    {
        if (args.Prototype.Spawns == null || args.Prototype.Spawns.Count == 0)
            return;

        foreach (var (entProto, targetMarkers) in args.Prototype.Spawns)
        {
            foreach (var markerId in targetMarkers)
            {
                if (string.IsNullOrWhiteSpace(markerId) || markerId.Equals("Global", StringComparison.OrdinalIgnoreCase))
                    Spawn(entProto, MapCoordinates.Nullspace);
            }
        }

        var query = EntityQueryEnumerator<CalendarSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var xform))
        {
            foreach (var (entProto, targetMarkers) in args.Prototype.Spawns)
            {
                foreach (var targetMarkerId in targetMarkers)
                {
                    if (targetMarkerId == marker.MarkerId)
                        Spawn(entProto, xform.Coordinates);
                }
            }
        }
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        _curCycle = 0;
        InitializeCalendarDecks();
    }

    private void InitializeCalendarDecks()
    {
        GenerateDeck(_dayDeck, DayTag, "DefaultCalendarEvent");
        GenerateDeck(_nightDeck, NightTag, "DefaultCalendarNightEvent");

        // Модифицировать колоду календаря можно после этого ивента
        RaiseLocalEvent(new CalendarDecksGeneratedEvent());
    }

    /// <summary>
    /// Позволяет внешним системам (например, игровым режимам) перезаписать конкретный день в колоде.
    /// </summary>
    /// <param name="dayNumber">Номер дня (начиная с 1)</param>
    /// <param name="eventId">ID прототипа события</param>
    /// <param name="isNight">Заменить событие в колоде ночей (true) или дней (false)</param>
    public void SetOverrideEvent(int dayNumber, ProtoId<CalendarEventPrototype> eventId, bool isNight = false)
    {
        var index = dayNumber - 1;

        if (isNight)
        {
            if (index >= 0 && index < _nightDeck.Count)
                _nightDeck[index] = eventId;
        }
        else
        {
            if (index >= 0 && index < _dayDeck.Count)
                _dayDeck[index] = eventId;
        }
    }

    private void GenerateDeck(List<ProtoId<CalendarEventPrototype>> deck, string filterTag, string fallbackEventId)
    {
        deck.Clear();

        var pool = new List<CalendarEventPrototype>();
        var playerCount = _playerManager.PlayerCount;

        foreach (var proto in _prototype.EnumeratePrototypes<CalendarEventPrototype>())
        {
            if (proto.Tags.Contains(filterTag) &&
                proto.Weight > 0f &&
                playerCount >= proto.MinPlayers &&
                playerCount <= proto.MaxPlayers)
            {
                pool.Add(proto);
            }
        }

        if (pool.Count == 0)
            return;

        var counts = new Dictionary<string, int>(pool.Count);
        var lastOccurrence = new Dictionary<string, int>(pool.Count);

        for (var day = 1; day <= TargetDaysCount; day++)
        {
            var candidates = pool.FindAll(p =>
                p.MinDay <= day &&
                counts.GetValueOrDefault(p.ID) < p.MaxOccurrences &&
                (day - lastOccurrence.GetValueOrDefault(p.ID, -999)) >= p.MinOffset
            );

            if (candidates.Count == 0)
            {
                candidates = pool.FindAll(p =>
                    p.MaxOccurrences >= TargetDaysCount &&
                    (day - lastOccurrence.GetValueOrDefault(p.ID, -999)) >= p.MinOffset
                );
            }

            if (candidates.Count == 0)
            {
                deck.Add(fallbackEventId);
                continue;
            }

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].GetWeight(day);
            }

            var roll = _random.NextFloat() * totalWeight;
            var acc = 0f;
            var selected = candidates[0];

            for (var i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].GetWeight(day);
                if (roll <= acc)
                {
                    selected = candidates[i];
                    break;
                }
            }

            deck.Add(selected.ID);
            counts[selected.ID] = counts.GetValueOrDefault(selected.ID) + 1;
            lastOccurrence[selected.ID] = day;
        }
    }

    private void OnDayCycleChanged(ref DayCycleStageChangedEvent args)
    {
        TriggerNextDayStageNotification(args.NextStage);
    }

    public void TriggerNextDayStageNotification(int stageNumber)
    {
        switch (stageNumber)
        {
            case DayStageNumber:
                {
                    _curCycle++;

                    if (_dayDeck.Count == 0)
                    {
                        TriggerDayStageNotification("DefaultCalendarEvent");
                        return;
                    }

                    var index = (_curCycle - 1) % _dayDeck.Count;
                    TriggerDayStageNotification(_dayDeck[index]);
                    break;
                }

            case NightStageNumber:
                {
                    if (_nightDeck.Count == 0)
                    {
                        TriggerDayStageNotification("DefaultCalendarNightEvent");
                        return;
                    }

                    var cycleIndex = Math.Max(0, _curCycle - 1);
                    var index = cycleIndex % _nightDeck.Count;
                    TriggerDayStageNotification(_nightDeck[index]);
                    break;
                }
        }
    }

    public void TriggerDayStageNotification(ProtoId<CalendarEventPrototype>? protoId = null)
    {
        var id = protoId ?? "DefaultCalendarEvent";

        if (!_prototype.TryIndex(id, out var proto))
            return;

        RaiseNetworkEvent(new CalendarBroadcastNotificationEvent(id), Filter.Broadcast());
        RaiseLocalEvent(new CalendarEventStartedEvent(_curCycle, id, proto));
    }
}


[AdminCommand(AdminFlags.VarEdit)]
public sealed class TriggerNextDayCycleCommand : IConsoleCommand
{
    public string Command => "triggernextdaycycle";
    public string Description => Loc.GetString("cmd-trigger-next-day-cycle-desc");
    public string Help => Loc.GetString("cmd-trigger-next-day-cycle-help", ("command", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var dayCount))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        var entSys = IoCManager.Resolve<IEntitySystemManager>();
        var daySystem = entSys.GetEntitySystem<CalendarSystem>();

        daySystem.TriggerNextDayStageNotification(dayCount);
        shell.WriteLine(Loc.GetString("cmd-trigger-next-day-cycle-success", ("days", dayCount)));
    }
}
