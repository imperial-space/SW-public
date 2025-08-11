using Content.Server.Imperial.Power.Components;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Power.EntitySystems.Events;

/// <summary>
/// Событие "Молния" - суперматерия генерирует электрические разряды
/// </summary>
public sealed class SupermatterLightningEvent
{
    public static void Activate(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system)
    {
        // Валидация входных параметров
        if (uid == EntityUid.Invalid)
        {
            system.Log.Error("SupermatterLightningEvent.Activate: Invalid EntityUid provided");
            return;
        }

        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventComponent.SupermatterEventType.Lightning;
        comp.EventEndTime = TimeSpan.FromSeconds(comp.LightningEventDuration);
        comp.NextEventTimer = TimeSpan.FromSeconds(system.Random.NextFloat(comp.LightningMinNextEvent, comp.LightningMaxNextEvent));
        comp.LightningCooldown = TimeSpan.Zero;
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
        comp.LastLightningCooldownUpdate = currentTime;

        // Стреляем молнии в случайные цели вокруг суперматерии
        ShootRandomLightnings(uid, system, comp);
    }

    public void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
    {
        var elapsedSinceLastUpdate = currentTime - comp.LastLightningCooldownUpdate;
        comp.LightningCooldown -= elapsedSinceLastUpdate;
        comp.LastLightningCooldownUpdate = currentTime;

        if (comp.LightningCooldown > TimeSpan.Zero)
            return;

        // Стреляем молнии в случайные цели вокруг суперматерии
        ShootRandomLightnings(uid, system, comp);

        if (system.TryGetComponent<SupermatterIntegrityComponent>(uid, out var integrity) && integrity != null &&
            system.TryGetComponent<DamageableComponent>(uid, out var _))
        {
            system.Damageable.TryChangeDamage(uid, integrity.TickDamage, origin: null);
        }

        comp.LightningCooldown = TimeSpan.FromSeconds(comp.LightningCooldownDuration);
    }

    private static void ShootRandomLightnings(EntityUid uid, SupermatterEventSystem system, SupermatterEventComponent component)
    {
        // Используем ShootRandomLightnings для стрельбы в случайные цели в радиусе 8 метров
        // 1 молния за раз, радиус 8 метров
        system.LightningSystem.ShootRandomLightnings(uid, component.LightningBoltRadius, component.LightningBoltCount);
    }

    public static string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-lightning");
    }
}
