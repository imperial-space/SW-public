using Robust.Shared.Localization;
using System;
using Content.Server.Imperial.Power.Components;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Радиация" - суперматерия излучает повышенную радиацию
/// </summary>
public sealed class SupermatterRadiationEvent : ISupermatterEvent
{
    public void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventType.Radiation;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.RadiationEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.RadiationMinNextEvent, comp.RadiationMaxNextEvent));
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;

        system.SetRadiation(uid, system.EntityManager, comp.RadiationIntensity);
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {
        // Поддерживаем радиацию на заданном уровне
        system.SetRadiation(uid, system.EntityManager, comp.RadiationIntensity);
    }

    public string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-radiation");
    }
}
