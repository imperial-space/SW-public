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
using System.Collections.Generic;

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

        // Кеш ближайших консолей для кристаллов
        private readonly Dictionary<EntityUid, (EntityUid console, float time)> _nearestConsoleCache = new();
        private const float ConsoleCacheLifetime = 10f; // секунд
        private float _consoleCacheTimer = 0f;

        private const float InitialEventDelaySeconds = 900f; // 15 минут

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterEventComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SupermatterEventComponent, SupermatterTouchedEvent>(OnTouched);
        }

        private void OnInit(EntityUid uid, SupermatterEventComponent comp, ComponentInit args)
        {
            comp.NextEventTimer = TimeSpan.FromSeconds(InitialEventDelaySeconds);
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
            _consoleCacheTimer += frameTime;
            if (_consoleCacheTimer >= ConsoleCacheLifetime)
            {
                _nearestConsoleCache.Clear();
                _consoleCacheTimer = 0f;
            }
            var enumerator = EntityQueryEnumerator<SupermatterEventComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var comp, out var xform))
            {
                var gas = _atmos.GetContainingMixture((uid, xform), true, false);
                var lastEvent = comp.CurrentEvent;
                if (comp.EventEndTime > TimeSpan.Zero)
                    comp.EventEndTime -= TimeSpan.FromSeconds(frameTime);
                if (comp.NextEventTimer > TimeSpan.Zero)
                    comp.NextEventTimer -= TimeSpan.FromSeconds(frameTime);
                if (comp.EventEndTime <= TimeSpan.Zero && comp.NextEventTimer <= TimeSpan.Zero)
                {
                    if (lastEvent == SupermatterEventType.Radiation && HasComp<RadiationSourceComponent>(uid))
                    {
                        var rad = Comp<RadiationSourceComponent>(uid);
                        rad.Intensity = 5f;
                    }
                    if (comp.AllowedEvents.Count == 0)
                        continue;
                    var evtIndex = _random.Next(0, comp.AllowedEvents.Count);
                    var evt = comp.AllowedEvents[evtIndex];
                    switch (evt)
                    {
                        case SupermatterLightningEvent lightningEvt:
                            lightningEvt.Activate(uid, EntityManager, comp, _random, _imperialLightning);
                            break;
                        case SupermatterRadiationEvent radiationEvt:
                            radiationEvt.Activate(uid, EntityManager, comp, _random);
                            break;
                        case SupermatterPlasmaEvent plasmaEvt:
                            plasmaEvt.Activate(uid, EntityManager, comp, _random);
                            break;
                        default:
                            evt.Activate(uid, EntityManager, comp);
                            break;
                    }
                    var msg = evt.GetAnnouncement(uid, EntityManager, comp);
                    AnnounceFromConsole(uid, msg);
                }
                if (comp.EventEndTime > TimeSpan.Zero)
                {
                    switch (comp.CurrentEvent)
                    {
                        case SupermatterEventType.Lightning:
                            ProcessLightningEvent(uid, comp, frameTime);
                            break;
                        case SupermatterEventType.Radiation:
                            ProcessRadiationEvent(uid);
                            break;
                        case SupermatterEventType.Plasma:
                            ProcessPlasmaEvent(uid, comp, xform, gas, frameTime);
                            break;
                    }
                }
            }
        }

        private void ProcessLightningEvent(EntityUid uid, SupermatterEventComponent comp, float frameTime)
        {
            comp.LightningCooldown -= TimeSpan.FromSeconds(frameTime);
            if (comp.LightningCooldown <= TimeSpan.Zero)
            {
                _imperialLightning.SpawnLightningBetween(uid, uid, null, null, TimeSpan.FromSeconds(1));
                if (EntityManager.TryGetComponent<SupermatterIntegrityComponent>(uid, out var integrity) &&
                    EntityManager.TryGetComponent<DamageableComponent>(uid, out var dmg))
                {
                    _damageable.TryChangeDamage(uid, integrity.TickDamage, false, true, origin: null);
                }
                comp.LightningCooldown = TimeSpan.FromSeconds(2);
            }
        }

        private void ProcessRadiationEvent(EntityUid uid)
        {
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
        }

        private void ProcessPlasmaEvent(EntityUid uid, SupermatterEventComponent comp, TransformComponent xform, GasMixture? gas, float frameTime)
        {
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
        }

        private void AnnounceFromConsole(EntityUid crystal, string message)
        {
            var gameTiming = IoCManager.Resolve<IGameTiming>();
            var now = (float)gameTiming.CurTime.TotalSeconds;
            EntityUid? nearestConsole = null;
            var pos = _transformSystem.GetMapCoordinates(crystal).Position;
            var mapId = _transformSystem.GetMapCoordinates(crystal).MapId;
            if (_nearestConsoleCache.TryGetValue(crystal, out var cached) && now - cached.time < ConsoleCacheLifetime)
            {
                nearestConsole = cached.console;
            }
            else
            {
                var minDist = float.MaxValue;
                var enumerator = EntityQueryEnumerator<SupermatterMonitorConsoleComponent, TransformComponent>();
                while (enumerator.MoveNext(out var consoleUid, out var cComp, out var cXform))
                {
                    if (cXform.MapID != mapId)
                        continue;
                    var cPos = _transformSystem.GetMapCoordinates(consoleUid).Position;
                    var dist = (cPos - pos).LengthSquared();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestConsole = consoleUid;
                    }
                }
                if (nearestConsole != null)
                    _nearestConsoleCache[crystal] = (nearestConsole.Value, now);
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
        // Реализация интерфейса для совместимости
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            var random = IoCManager.Resolve<IRobustRandom>();
            var imperialLightning = IoCManager.Resolve<ImperialLightningSystem>();
            Activate(crystal, entityManager, comp, random, imperialLightning);
        }
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp, IRobustRandom random, ImperialLightningSystem imperialLightning)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(random.NextFloat(180f, 420f));
            comp.LightningCooldown = TimeSpan.Zero;
            imperialLightning.SpawnLightningBetween(crystal, crystal, null, null, TimeSpan.FromSeconds(1));
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-lightning");
        }
    }

    public sealed class SupermatterRadiationEvent : ISupermatterEvent
    {
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            var random = IoCManager.Resolve<IRobustRandom>();
            Activate(crystal, entityManager, comp, random);
        }
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp, IRobustRandom random)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(random.NextFloat(180f, 420f));
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
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            var random = IoCManager.Resolve<IRobustRandom>();
            Activate(crystal, entityManager, comp, random);
        }
        public void Activate(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp, IRobustRandom random)
        {
            comp.EventEndTime = TimeSpan.FromSeconds(120);
            comp.NextEventTimer = TimeSpan.FromSeconds(random.NextFloat(180f, 420f));
        }
        public string GetAnnouncement(EntityUid crystal, EntityManager entityManager, SupermatterEventComponent comp)
        {
            return Loc.GetString("supermatter-event-plasma");
        }
    }
}
