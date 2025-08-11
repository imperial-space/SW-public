using Content.Server.Imperial.Power.Components;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Ничего" - период спокойствия суперматерии
/// </summary>
public sealed class SupermatterNoneEvent
{
    public static void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {

        if (uid == EntityUid.Invalid)
        {
            system.Log.Error("SupermatterNoneEvent.Activate: Invalid EntityUid provided");
            return;
        }

        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventComponent.SupermatterEventType.None;
        comp.EventEndTime = TimeSpan.Zero;
        comp.NextEventTimer = TimeSpan.FromSeconds(comp.NoneEventDuration);
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {

    }

    public static string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-none");
    }
}
