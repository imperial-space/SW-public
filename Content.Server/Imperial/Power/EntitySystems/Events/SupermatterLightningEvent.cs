using Robust.Shared.Localization;
using System;
using Content.Server.Imperial.Power.Components;
using Content.Server.Imperial.ImperialLightning;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Молния" - суперматерия генерирует электрические разряды
/// </summary>
public sealed class SupermatterLightningEvent : ISupermatterEvent
{
    public void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventType.Lightning;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.LightningEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.LightningMinNextEvent, comp.LightningMaxNextEvent));
        comp.LightningCooldown = TimeSpan.Zero;
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
        comp.LastLightningCooldownUpdate = currentTime;

        system.ImperialLightning?.SpawnLightningBetween(uid, uid, null, null, TimeSpan.FromSeconds(comp.LightningSpawnDuration));
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {
        var elapsedSinceLastUpdate = currentTime - comp.LastLightningCooldownUpdate;
        comp.LightningCooldown -= elapsedSinceLastUpdate;
        comp.LastLightningCooldownUpdate = currentTime;

        if (comp.LightningCooldown <= TimeSpan.Zero)
        {
            system.ImperialLightning?.SpawnLightningBetween(uid, uid, null, null, TimeSpan.FromSeconds(comp.LightningSpawnDuration));

            if (system.EntityManager.TryGetComponent<SupermatterIntegrityComponent>(uid, out var integrity) &&
                system.EntityManager.TryGetComponent<DamageableComponent>(uid, out var dmg))
            {
                system.Damageable.TryChangeDamage(uid, integrity.TickDamage, false, true, origin: null);
            }

            comp.LightningCooldown = TimeSpan.FromSeconds(comp.LightningCooldownDuration);
        }
    }

    public string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-lightning");
    }
}
