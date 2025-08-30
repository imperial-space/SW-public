using Content.Server.Imperial.Power.Components;
using Content.Shared.Damage;
using Robust.Shared.Localization;
using System;

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
        if (comp == null)
        {
            return;
        }

        if (system == null)
        {
            return;
        }

        // Валидация конфигурации компонента
        if (comp.LightningEventDuration <= TimeSpan.Zero)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid LightningEventDuration: {comp.LightningEventDuration}");
            return;
        }

        if (comp.LightningCooldownDuration <= TimeSpan.Zero)
        {
            system.Log.Warning($"SupermatterLightningEvent.Activate: Invalid LightningCooldownDuration: {comp.LightningCooldownDuration}");
            return;
        }

        var currentTime = system.GameTiming.CurTime;
        comp.CurrentEvent = SupermatterEventComponent.SupermatterEventType.Lightning;
        comp.EventEndTime = comp.LightningEventDuration;
        comp.NextEventTimer = comp.EventAfterLightingTime;
        comp.LightningCooldown = TimeSpan.Zero;
        comp.LastEventEndTimeUpdate = currentTime;
        comp.LastNextEventTimerUpdate = currentTime;
        comp.LastLightningCooldownUpdate = currentTime;

        // Стреляем молнии в случайные цели вокруг суперматерии
        ShootRandomLightnings(uid, system, comp);
    }

    public static void Process(EntityUid uid, SupermatterEventComponent comp, SupermatterEventSystem system, TimeSpan currentTime)
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

        comp.LightningCooldown = comp.LightningCooldownDuration;
    }

    private static void ShootRandomLightnings(EntityUid uid, SupermatterEventSystem system, SupermatterEventComponent component)
    {
        // Используем ShootRandomLightnings для стрельбы в случайные цели в радиусе
        system.LightningSystem?.ShootRandomLightnings(uid, component.LightningBoltRadius, component.LightningBoltCount, "Lightning", 0, true);
    }

    public static string GetAnnouncement()
    {
        return Loc.GetString("supermatter-event-lightning");
    }
}
