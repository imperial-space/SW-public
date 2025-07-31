using Robust.Shared.Localization;
using System;
using Content.Server.Imperial.Power.Components;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Ничего" - период спокойствия суперматерии
/// </summary>
public sealed class SupermatterNoneEvent : ISupermatterEvent
{
    public void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventType.None;
        comp.EventEndTime = TimeSpan.Zero;
        comp.NextEventTimer = TimeSpan.FromSeconds(comp.NoneEventDuration);
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {
        // None событие не требует обработки во время выполнения
    }

    public string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-none");
    }
}
