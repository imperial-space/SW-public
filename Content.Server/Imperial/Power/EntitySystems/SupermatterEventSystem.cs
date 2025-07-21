using Content.Server.Imperial.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Content.Server.Lightning;
using Content.Server.Radiation.Systems;
using Robust.Shared.Random;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using System;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterEventSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmos = default!;
        [Dependency] private readonly RadioSystem _radio = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly LightningSystem _lightning = default!;
        [Dependency] private readonly RadiationSystem _radiation = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly IGameTiming Timing = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterEventComponent, ComponentInit>(OnInit);
        }

        private void OnInit(EntityUid uid, SupermatterEventComponent comp, ComponentInit args)
        {
            comp.NextEventTimer = 900f; // 15 минут
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var (comp, xform) in EntityQuery<SupermatterEventComponent, TransformComponent>())
            {
                var uid = xform.Owner;
                var gas = _atmos.GetContainingMixture((uid, xform), true, false);

                // Сохраняем предыдущее событие
                int lastEvent = comp.CurrentEvent;

                // Принудительный запуск события
                if (comp.ForceEvent)
                {
                    comp.EventEndTime = 0f;
                    comp.NextEventTimer = 0f;
                    comp.ForceEvent = false;
                }

                // Сначала уменьшаем EventEndTime
                if (comp.EventEndTime > 0f)
                    comp.EventEndTime -= frameTime;
                // Затем NextEventTimer
                if (comp.NextEventTimer > 0f)
                    comp.NextEventTimer -= frameTime;

                // Запуск нового события
                if (comp.EventEndTime <= 0f && comp.NextEventTimer <= 0f)
                {
                    // Если только что закончилось радиационное событие — сбросить радиацию
                    if (lastEvent == 2 && HasComp<RadiationSourceComponent>(uid))
                    {
                        var rad = Comp<RadiationSourceComponent>(uid);
                        rad.Intensity = 5f;
                    }
                    int evt = _random.Next(0, 4);
                    comp.CurrentEvent = evt;
                    comp.EventEndTime = evt == 0 ? 0f : 120f;
                    comp.NextEventTimer = _random.NextFloat(180f, 420f);
                    comp.ForceEvent = false;
                    if (evt == 1)
                        comp.LightningCooldown = 0f;
                    string msg = evt switch
                    {
                        0 => "Суперматерия стабилизирована. Нет аномальных всплесков.",
                        1 => "ВНИМАНИЕ: Зафиксирован энергетический всплеск! Кристалл выпускает молнии!",
                        2 => "ВНИМАНИЕ: Зафиксирован радиационный выброс! Уровень радиации повышен!",
                        3 => "ВНИМАНИЕ: Зафиксирован выброс плазмы! Вокруг кристалла появляется горящая плазма!",
                        _ => ""
                    };
                    AnnounceFromConsole(uid, msg);
                }

                // Активные эффекты событий
                if (comp.EventEndTime > 0f)
                {
                    switch (comp.CurrentEvent)
                    {
                        case 1: // Молнии
                            comp.LightningCooldown -= frameTime;
                            if (comp.LightningCooldown <= 0f)
                            {
                                _lightning.ShootRandomLightnings(uid, 8f, _random.Next(2, 5), arcDepth: 2);
                                comp.LightningCooldown = 2f;
                            }
                            break;
                        case 2: // Радиоактивный выброс
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
                        case 3: // Плазма
                            if ((int)comp.EventEndTime % 10 == 0 && gas != null)
                            {
                                gas.AdjustMoles((int)Gas.Plasma, 5f);
                                gas.AdjustMoles((int)Gas.Oxygen, 5f);
                                var coords = xform.Coordinates;
                                var gridUid = xform.GridUid!.Value;
                                var grid = Comp<MapGridComponent>(gridUid);
                                var tile = _mapSystem.TileIndicesFor(gridUid, grid, coords);
                                _atmos.HotspotExpose(gridUid, tile, 1500f, 50f, uid, true);
                            }
                            break;
                    }
                }
                else
                {
                    // Сбросить эффекты после окончания события (оставляем только для молний)
                    if (comp.CurrentEvent == 1)
                        comp.LightningCooldown = 0f;
                    comp.CurrentEvent = 0;
                }
            }
        }

        private void AnnounceFromConsole(EntityUid crystal, string message)
        {
            EntityUid? nearestConsole = null;
            float minDist = float.MaxValue;
            var xform = Transform(crystal);
            var pos = xform.MapPosition.Position;
            foreach (var console in EntityManager.EntityQuery<SupermatterMonitorConsoleComponent>())
            {
                var cXform = Transform(console.Owner);
                if (cXform.MapID != xform.MapID)
                    continue;
                var cPos = cXform.MapPosition.Position;
                var dist = (cPos - pos).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestConsole = console.Owner;
                }
            }
            _radio.SendRadioMessage(nearestConsole ?? crystal, message, "Engineering", nearestConsole ?? crystal);
        }
    }
}
