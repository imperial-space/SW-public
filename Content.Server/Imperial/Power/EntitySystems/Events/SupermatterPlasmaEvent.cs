using Robust.Shared.Localization;
using System;
using Content.Shared.Atmos;
using Content.Server.Imperial.Power.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Плазма" - суперматерия генерирует плазму
/// </summary>
public sealed class SupermatterPlasmaEvent : ISupermatterEvent
{
    public void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventType.Plasma;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.PlasmaEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.PlasmaMinNextEvent, comp.PlasmaMaxNextEvent));
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
        comp.LastPlasmaTickUpdate = currentTime;
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {
        if (!comp.PlasmaTickAccumulator.HasValue)
            comp.PlasmaTickAccumulator = TimeSpan.Zero;

        var elapsedSinceLastUpdate = currentTime - comp.LastPlasmaTickUpdate;
        comp.PlasmaTickAccumulator += elapsedSinceLastUpdate;
        comp.LastPlasmaTickUpdate = currentTime;

        if (comp.PlasmaTickAccumulator >= TimeSpan.FromSeconds(comp.PlasmaTickInterval))
        {
            var gas = system.Atmos.GetContainingMixture((uid, system.EntityManager.GetComponent<TransformComponent>(uid)), true, false);
            if (gas != null)
            {
                gas.AdjustMoles((int)Gas.Plasma, comp.PlasmaMolesAmount);
                gas.AdjustMoles((int)Gas.Oxygen, comp.PlasmaMolesAmount);

                var coords = system.EntityManager.GetComponent<TransformComponent>(uid).Coordinates;
                var xform = system.EntityManager.GetComponent<TransformComponent>(uid);

                if (xform.GridUid == null)
                {
                    system.Log.Warning($"Supermatter plasma event triggered for entity {uid} without grid");
                    return;
                }

                var gridUid = xform.GridUid.Value;
                var grid = system.EntityManager.GetComponent<MapGridComponent>(gridUid);
                var tile = system.MapSystem.TileIndicesFor(gridUid, grid, coords);
                system.Atmos.HotspotExpose(gridUid, tile, comp.PlasmaHotspotTemperature, comp.PlasmaHotspotVolume, uid, true);
            }

            comp.PlasmaTickAccumulator -= TimeSpan.FromSeconds(comp.PlasmaTickInterval);
        }
    }

    public string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-plasma");
    }
}

