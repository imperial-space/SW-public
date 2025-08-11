using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Power.Components;
using Content.Server.Imperial.Power.EntitySystems.Events;
using Content.Server.Radio.EntitySystems;
using Content.Server.Lightning;
using Content.Shared.Damage;
using Content.Shared.Radiation.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterEventSystem : EntitySystem
    {
        [Dependency] public readonly AtmosphereSystem Atmos = default!;
        [Dependency] private readonly RadioSystem _radio = default!;
        [Dependency] public readonly IRobustRandom Random = default!;
        [Dependency] public readonly SharedMapSystem MapSystem = default!;
        [Dependency] public readonly LightningSystem LightningSystem = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] public readonly DamageableSystem Damageable = default!;
        [Dependency] public readonly IGameTiming GameTiming = default!;

        // Словарь событий для быстрого доступа
        private readonly Dictionary<SupermatterEventComponent.SupermatterEventType, object> _events = new()
        {
            { SupermatterEventComponent.SupermatterEventType.None, new SupermatterNoneEvent() },
            { SupermatterEventComponent.SupermatterEventType.Lightning, new SupermatterLightningEvent() },
            { SupermatterEventComponent.SupermatterEventType.Radiation, new SupermatterRadiationEvent() },
            { SupermatterEventComponent.SupermatterEventType.Plasma, new SupermatterPlasmaEvent() }
        };

        // Кеш ближайших консолей для кристаллов
        private readonly Dictionary<EntityUid, (EntityUid console, float time)> _nearestConsoleCache = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterEventComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SupermatterEventComponent, SupermatterTouchedEvent>(OnTouched);
        }

        private void OnInit(EntityUid uid, SupermatterEventComponent comp, ComponentInit args)
        {
            var currentTime = GameTiming.CurTime;
            comp.NextEventTimer = TimeSpan.FromSeconds(comp.InitialEventDelaySeconds); // 15 минут до первого ивента
            comp.LastConsoleCacheUpdate = currentTime;
            comp.LastEventEndTimeUpdate = currentTime;
            comp.LastNextEventTimerUpdate = currentTime;
            comp.LastLightningCooldownUpdate = currentTime;
            comp.LastPlasmaTickUpdate = currentTime;
        }

        private void OnTouched(EntityUid uid, SupermatterEventComponent comp, SupermatterTouchedEvent args)
        {
            TriggerEventNow(uid);
        }

        private void TriggerEventNow(EntityUid uid)
        {
            if (!EntityManager.TryGetComponent<SupermatterEventComponent>(uid, out var comp))
                return;
            comp.EventEndTime = TimeSpan.Zero;
            comp.NextEventTimer = TimeSpan.Zero;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTime = GameTiming.CurTime;
            var enumerator = EntityQueryEnumerator<SupermatterEventComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var comp, out _))
            {
                // Очистка кэша консоли
                if (currentTime - comp.LastConsoleCacheUpdate >= TimeSpan.FromSeconds(comp.ConsoleCacheLifetime))
                {
                    _nearestConsoleCache.Clear();
                    comp.LastConsoleCacheUpdate = currentTime;
                }

                var lastEvent = comp.CurrentEvent;

                // Обновление времени окончания события
                if (comp.EventEndTime > TimeSpan.Zero)
                {
                    var elapsedSinceLastUpdate = currentTime - comp.LastEventEndTimeUpdate;
                    comp.EventEndTime -= elapsedSinceLastUpdate;
                    comp.LastEventEndTimeUpdate = currentTime;
                }

                // Обновление таймера до следующего события
                if (comp.NextEventTimer > TimeSpan.Zero)
                {
                    var elapsedSinceLastUpdate = currentTime - comp.LastNextEventTimerUpdate;
                    comp.NextEventTimer -= elapsedSinceLastUpdate;
                    comp.LastNextEventTimerUpdate = currentTime;
                }

                if (comp.EventEndTime <= TimeSpan.Zero && comp.NextEventTimer <= TimeSpan.Zero)
                {
                    if (lastEvent == SupermatterEventComponent.SupermatterEventType.Radiation && HasComp<RadiationSourceComponent>(uid))
                    {
                        var rad = Comp<RadiationSourceComponent>(uid);
                        rad.Intensity = comp.DefaultRadiationIntensity;
                    }

                    if (comp.AllowedEventTypes.Count == 0)
                        continue;

                    var evtIndex = Random.Next(0, comp.AllowedEventTypes.Count);
                    var evtType = comp.AllowedEventTypes[evtIndex];

                    if (_events.TryGetValue(evtType, out var eventHandler))
                    {
                        switch (eventHandler)
                        {
                            case SupermatterNoneEvent:
                                SupermatterNoneEvent.Activate(uid, comp, this);
                                AnnounceFromConsole(uid, SupermatterNoneEvent.GetAnnouncement());
                                break;
                            case SupermatterLightningEvent:
                                SupermatterLightningEvent.Activate(uid, comp, this);
                                AnnounceFromConsole(uid, SupermatterLightningEvent.GetAnnouncement());
                                break;
                            case SupermatterRadiationEvent:
                                SupermatterRadiationEvent.Activate(uid, comp, this);
                                AnnounceFromConsole(uid, SupermatterRadiationEvent.GetAnnouncement());
                                break;
                            case SupermatterPlasmaEvent:
                                SupermatterPlasmaEvent.Activate(uid, comp, this);
                                AnnounceFromConsole(uid, SupermatterPlasmaEvent.GetAnnouncement());
                                break;
                        }
                    }
                }

                if (comp.EventEndTime <= TimeSpan.Zero)
                    continue;

                if (_events.TryGetValue(comp.CurrentEvent, out var newEventHandler))
                {
                    switch (newEventHandler)
                    {
                        case SupermatterNoneEvent noneEvent:
                            noneEvent.Process(uid, comp, this, currentTime);
                            break;
                        case SupermatterLightningEvent lightningEvent:
                            lightningEvent.Process(uid, comp, this, currentTime);
                            break;
                        case SupermatterRadiationEvent radiationEvent:
                            radiationEvent.Process(uid, comp, this, currentTime);
                            break;
                        case SupermatterPlasmaEvent plasmaEvent:
                            plasmaEvent.Process(uid, comp, this, currentTime);
                            break;
                    }
                }
            }
        }

        private void AnnounceFromConsole(EntityUid crystal, string message)
        {
            var now = (float)GameTiming.CurTime.TotalSeconds;
            EntityUid? nearestConsole = null;
            var pos = _transformSystem.GetMapCoordinates(crystal).Position;
            var mapId = _transformSystem.GetMapCoordinates(crystal).MapId;

            if (!EntityManager.TryGetComponent<SupermatterEventComponent>(crystal, out var eventComp))
                return;

            if (_nearestConsoleCache.TryGetValue(crystal, out var cached) && now - cached.time < eventComp.ConsoleCacheLifetime)
            {
                nearestConsole = cached.console;
            }
            else
            {
                var minDist = float.MaxValue;
                var enumerator = EntityQueryEnumerator<SupermatterMonitorConsoleComponent, TransformComponent>();
                while (enumerator.MoveNext(out var consoleUid, out _, out var transformComp))
                {
                    if (transformComp.MapID != mapId)
                        continue;
                    var cPos = _transformSystem.GetMapCoordinates(consoleUid).Position;
                    var dist = (cPos - pos).LengthSquared();

                    if (!(dist < minDist))
                        continue;

                    minDist = dist;
                    nearestConsole = consoleUid;
                }
                if (nearestConsole != null)
                    _nearestConsoleCache[crystal] = (nearestConsole.Value, now);
            }

            foreach (var channel in eventComp.RadioChannels)
            {
                _radio.SendRadioMessage(nearestConsole ?? crystal, message, channel, nearestConsole ?? crystal);
            }
        }

        public void SetRadiation(EntityUid uid, float intensity)
        {
            if (EntityManager.TryGetComponent<RadiationSourceComponent>(uid, out var radComponent))
                radComponent.Intensity = intensity;
            else
            {
                var newRad = EntityManager.EnsureComponent<RadiationSourceComponent>(uid);
                newRad.Intensity = intensity;
            }
        }

        public bool TryGetComponent<T>(EntityUid uid, out T? component) where T : IComponent
        {
            return EntityManager.TryGetComponent(uid, out component);
        }
    }
}
