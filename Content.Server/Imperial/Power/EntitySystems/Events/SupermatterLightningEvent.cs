using Content.Server.Imperial.Power.Components;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Молния" - суперматерия генерирует электрические разряды
/// </summary>
public sealed class SupermatterLightningEvent
{
    public static void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem supermatterSystem)
    {
        // Валидация входных параметров
        if (uid == EntityUid.Invalid)
        {
            supermatterSystem.Log.Error("SupermatterLightningEvent.Activate: Invalid EntityUid provided");
            return;
        }
        if (comp == null)
        {
            return;
        }

        if (system == null)
        {
            return;
        }

        // Валидация конфигурации компонента
        if (comp.LightningEventDuration <= 0)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid LightningEventDuration: {comp.LightningEventDuration}");
            return;
        }

        if (comp.LightningMinNextEvent <= 0 || comp.LightningMaxNextEvent <= 0)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid next event range: min={comp.LightningMinNextEvent}, max={comp.LightningMaxNextEvent}");
            return;
        }

        if (comp.LightningMinNextEvent > comp.LightningMaxNextEvent)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Min next event time greater than max: min={comp.LightningMinNextEvent}, max={comp.LightningMaxNextEvent}");
            return;
        }

        if (comp.LightningSpawnDuration <= 0)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid LightningSpawnDuration: {comp.LightningSpawnDuration}");
            return;
        }

        if (comp.LightningCooldownDuration <= 0)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid LightningCooldownDuration: {comp.LightningCooldownDuration}");
            return;
        }

        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventType.Lightning;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.LightningEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.LightningMinNextEvent, comp.LightningMaxNextEvent));
=======
        var currentTime = supermatterSystem.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventComponent.SupermatterEventType.Lightning;
        comp.EventEndTime = comp.LightningEventDuration;
        comp.NextEventTimer = comp.EventAfterLightingTime;
>>>>>>> develop
        comp.LightningCooldown = TimeSpan.Zero;
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
        comp.LastLightningCooldownUpdate = currentTime;

        // Стреляем молнии в случайные цели вокруг суперматерии
        ShootRandomLightnings(uid, supermatterSystem, comp);
    }

    public static void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem supermatterSystem, TimeSpan currentTime)
    {
        var elapsedSinceLastUpdate = currentTime - comp.LastLightningCooldownUpdate;
        comp.LightningCooldown -= elapsedSinceLastUpdate;
        comp.LastLightningCooldownUpdate = currentTime;

        if (comp.LightningCooldown > TimeSpan.Zero)
            return;

        // Стреляем молнии в случайные цели вокруг суперматерии
        ShootRandomLightnings(uid, supermatterSystem, comp);

        if (supermatterSystem.TryGetComponent<SupermatterIntegrityComponent>(uid, out var integrity) && integrity != null &&
            supermatterSystem.TryGetComponent<DamageableComponent>(uid, out var _))
        {
            supermatterSystem.Damageable.TryChangeDamage(uid, integrity.TickDamage, origin: null);
        }

        comp.LightningCooldown = comp.LightningCooldownDuration;
    }

    private static void ShootRandomLightnings(EntityUid uid, SupermatterEventSystem supermatterSystem, SupermatterEventComponent component)
    {
        // Используем ShootRandomLightnings для стрельбы в случайные цели в радиусе
        supermatterSystem.LightningSystem.ShootRandomLightnings(uid, component.LightningBoltRadius, component.LightningBoltCount);
    }

    public static string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-lightning");
    }
}
