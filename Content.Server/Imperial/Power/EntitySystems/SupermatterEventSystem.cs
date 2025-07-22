using Content.Server.Imperial.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Content.Server.Lightning;
using Robust.Shared.Random;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using System;
using Robust.Shared.Localization;
using Content.Server.Imperial.ImperialLightning;
using Robust.Server.GameObjects;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterEventSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmos = default!;
        [Dependency] private readonly RadioSystem _radio = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly LightningSystem _lightning = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly ImperialLightningSystem _imperialLightning = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;

        public override void Initialize()
        {
            SupermatterLightningEvent.Random = _random;
            SupermatterLightningEvent.ImperialLightning = _imperialLightning;
            SupermatterRadiationEvent.Random = _random;
            SupermatterPlasmaEvent.Random = _random;
            base.Initialize();
            SubscribeLocalEvent<SupermatterEventComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SupermatterEventComponent, SupermatterTouchedEvent>(OnTouched);
        }

        private void OnInit(EntityUid uid, SupermatterEventComponent comp, ComponentInit args)
        {
            comp.NextEventTimer = TimeSpan.FromSeconds(900f); // 15 минут
            comp.AllowedEvents.Clear();
            comp.AllowedEvents.Add(new SupermatterNoneEvent());
            comp.AllowedEvents.Add(new SupermatterLightningEvent());
            comp.AllowedEvents.Add(new SupermatterRadiationEvent());
            comp.AllowedEvents.Add(new SupermatterPlasmaEvent());
        }

        private void OnTouched(EntityUid uid, SupermatterEventComponent comp, SupermatterTouchedEvent args)
        {
            TriggerEventNow(uid);
        }

        public void TriggerEventNow(EntityUid uid)
        {
            if (!EntityManager.TryGetComponent<SupermatterEventComponent>(uid, out var comp))
                return;
            comp.EventEndTime = TimeSpan.Zero;
            comp.NextEventTimer = TimeSpan.Zero;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var enumerator = EntityQueryEnumerator<SupermatterEventComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var comp, out var xform))
            {
                var gas = _atmos.GetContainingMixture((uid, xform), true, false);

                // Сохраняем предыдущее событие
                var lastEvent = comp.CurrentEvent;

                // Сначала уменьшаем EventEndTime
                if (comp.EventEndTime > TimeSpan.Zero)
                    comp.EventEndTime -= TimeSpan.FromSeconds(frameTime);
                // Затем NextEventTimer
                if (comp.NextEventTimer > TimeSpan.Zero)
                    comp.NextEventTimer -= TimeSpan.FromSeconds(frameTime);

                // Запуск нового события
                if (comp.EventEndTime <= TimeSpan.Zero && comp.NextEventTimer <= TimeSpan.Zero)
                {
                    // Если только что закончилось радиационное событие — сбросить радиацию
                    if (lastEvent == SupermatterEventType.Radiation && HasComp<RadiationSourceComponent>(uid))
                    {
                        var rad = Comp<RadiationSourceComponent>(uid);
                        rad.Intensity = 5f;
                    }
                    // Выбор события из AllowedEvents
                    if (comp.AllowedEvents.Count == 0)
                        continue;
                    var evtIndex = _random.Next(0, comp.AllowedEvents.Count);
                    var evt = comp.AllowedEvents[evtIndex];
                    evt.Activate(uid, EntityManager, comp);
                    var msg = evt.GetAnnouncement(uid, EntityManager, comp);
                    AnnounceFromConsole(uid, msg);
                }

                // Активные эффекты событий
                if (comp.EventEndTime > TimeSpan.Zero)
                {
                    switch (comp.CurrentEvent)
                    {
                        case SupermatterEventType.Lightning:
                            comp.LightningCooldown -= TimeSpan.FromSeconds(frameTime);
                            if (comp.LightningCooldown <= TimeSpan.Zero)
                            {
                                _imperialLightning.SpawnLightningBetween(uid, uid, null, null, TimeSpan.FromSeconds(1));
                                // Наносим урон суперматерии при каждом срабатывании молний
                                if (EntityManager.TryGetComponent<SupermatterIntegrityComponent>(uid, out var integrity) &&
                                    EntityManager.TryGetComponent<DamageableComponent>(uid, out var dmg))
                                {
                                    _damageable.TryChangeDamage(uid, integrity.TickDamage, false, true, origin: null);
                                }
                                comp.LightningCooldown = TimeSpan.FromSeconds(2);
                            }
                            break;
                        case SupermatterEventType.Radiation:
                            if (HasComp<RadiationSourceComponent>(uid))
                            {
                                var rad = Comp<RadiationSourceComponent>(uid);
                                rad.Intensity = 10f;
                            }
                            else
                            {
                                var rad = EnsureComp<RadiationSourceComponent>(uid);
                                rad.Intensity = 10f;
                            }
                            break;
                        case SupermatterEventType.Plasma:
                            if (!comp.PlasmaTickAccumulator.HasValue)
                                comp.PlasmaTickAccumulator = TimeSpan.Zero;
                            comp.PlasmaTickAccumulator += TimeSpan.FromSeconds(frameTime);
                            if (comp.PlasmaTickAccumulator >= TimeSpan.FromSeconds(10) && gas != null)
                            {
                                gas.AdjustMoles((int)Gas.Plasma, 5f);
                                gas.AdjustMoles((int)Gas.Oxygen, 5f);
                                var coords = xform.Coordinates;
                                var gridUid = xform.GridUid!.Value;
                                var grid = Comp<MapGridComponent>(gridUid);
                                var tile = _mapSystem.TileIndicesFor(gridUid, grid, coords);
                                _atmos.HotspotExpose(gridUid, tile, 1500f, 50f, uid, true);
                                comp.PlasmaTickAccumulator -= TimeSpan.FromSeconds(10);
                            }
                            break;
                    }
                }
                else
                {
                    comp.PlasmaTickAccumulator = null;
                    // Сбросить эффекты после окончания события (оставляем только для молний)
                    if (comp.CurrentEvent == SupermatterEventType.Lightning)
                        comp.LightningCooldown = TimeSpan.Zero;
                    comp.CurrentEvent = SupermatterEventType.None;
                }
            }
        }

        private void AnnounceFromConsole(EntityUid crystal, string message)
        {
            var nearestConsole = (EntityUid?)null;
            var minDist = float.MaxValue;
            var pos = _transformSystem.GetMapCoordinates(crystal).Position;
            var enumerator = EntityQueryEnumerator<SupermatterMonitorConsoleComponent, TransformComponent>();
            while (enumerator.MoveNext(out var consoleUid, out var cComp, out var cXform))
            {
                if (cXform.MapID != _transformSystem.GetMapCoordinates(crystal).MapId)
                    continue;
                var cPos = _transformSystem.GetMapCoordinates(consoleUid).Position;
                var dist = (cPos - pos).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestConsole = consoleUid;
                }
            }
            if (EntityManager.TryGetComponent<SupermatterEventComponent>(crystal, out var eventComp))
            {
                foreach (var channel in eventComp.RadioChannels)
                {
                    _radio.SendRadioMessage(nearestConsole ?? crystal, message, channel, nearestConsole ?? crystal);
                }
            }
        }
    }

    public sealed class SupermatterNoneEvent : ISupermatterEvent
    {
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            // Нет эффекта
            comp.EventEndTime = TimeSpan.Zero;
            comp.NextEventTimer = TimeSpan.FromSeconds(300); // 5 минут до следующего события
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-none");
        }
    }

    public sealed class SupermatterLightningEvent : ISupermatterEvent
    {
        public static IRobustRandom? Random = null;
        public static ImperialLightningSystem? ImperialLightning = null;
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(Random!.NextFloat(180f, 420f));
            comp.LightningCooldown = TimeSpan.Zero;
            // Красивые молнии через ImperialLightningSystem
            ImperialLightning!.SpawnLightningBetween(crystal, crystal, null, null, TimeSpan.FromSeconds(1));
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-lightning");
        }
    }

    public sealed class SupermatterRadiationEvent : ISupermatterEvent
    {
        public static IRobustRandom? Random = null;
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(Random!.NextFloat(180f, 420f));
            if (entityManager.TryGetComponent<RadiationSourceComponent>(crystal, out var radComponent))
                radComponent.Intensity = 10f;
            else
            {
                var newRad = entityManager.EnsureComponent<RadiationSourceComponent>(crystal);
                newRad.Intensity = 10f;
            }
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-radiation");
        }
    }

    public sealed class SupermatterPlasmaEvent : ISupermatterEvent
    {
        public static IRobustRandom? Random = null;
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(Random!.NextFloat(180f, 420f));
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-plasma");
        }
    }
}
