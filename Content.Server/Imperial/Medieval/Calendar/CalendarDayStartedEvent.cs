using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Calendar;

public sealed class CalendarEventStartedEvent : EntityEventArgs
{
    public int DayNumber { get; }
    public ProtoId<CalendarEventPrototype> EventId { get; }
    public CalendarEventPrototype Prototype { get; }

    public CalendarEventStartedEvent(int dayNumber, ProtoId<CalendarEventPrototype> eventId, CalendarEventPrototype prototype)
    {
        DayNumber = dayNumber;
        EventId = eventId;
        Prototype = prototype;
    }
}

/// <summary>
/// Вызывается сразу после того, как CalendarSystem сгенерировал базовые колоды дней и ночей на раунд.
/// Идеальное место для игровых режимов, чтобы перезаписать нужные дни.
/// </summary>
public sealed class CalendarDecksGeneratedEvent : EntityEventArgs
{
}
