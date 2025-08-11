using Content.Server.Imperial.Power.Components;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Радиация" - суперматерия излучает повышенную радиацию
/// </summary>
public sealed class SupermatterRadiationEvent
{
    public static void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        // Валидация входных параметров
        if (uid == EntityUid.Invalid)
        {
            system.Log.Error("SupermatterRadiationEvent.Activate: Invalid EntityUid provided");
            return;
        }

        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventComponent.SupermatterEventType.Radiation;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.RadiationEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.RadiationMinNextEvent, comp.RadiationMaxNextEvent));
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;

        // Обработка потенциальных исключений при установке радиации
        system.SetRadiation(uid, comp.RadiationIntensity);
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem supermatterSystem, TimeSpan currentTime)
    {
        supermatterSystem.SetRadiation(uid, comp.RadiationIntensity);
    }

    public static string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-radiation");
    }
}
