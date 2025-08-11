using Content.Server.Imperial.Power.Components;
using Content.Shared.Examine;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Robust.Shared.Physics.Events;
using Content.Server.Chat.Systems;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Server.Explosion.EntitySystems;
using System.Linq;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterIntegritySystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly RadioSystem _radioSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterIntegrityComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<SupermatterIntegrityComponent, StartCollideEvent>(OnStartCollide);
            SubscribeLocalEvent<SupermatterIntegrityComponent, ProjectileHitEvent>(OnProjectileHit);
        }

        private void OnProjectileHit(EntityUid uid, SupermatterIntegrityComponent comp, ref ProjectileHitEvent args)
        {
            comp.Integrity = MathF.Max(0, comp.Integrity - 10f); // 10 урона за выстрел
        }

        private void OnExamined(EntityUid uid, SupermatterIntegrityComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            var integrityPercent = component.Integrity / component.MaxIntegrity * 100;
            foreach (var (key, descId) in component.IntegrityDescription.OrderByDescending(p => p.Key))
            {
                if (integrityPercent < key)
                    continue;

                args.PushMarkup(Loc.GetString(descId));
                break;
            }
        }

        private void OnStartCollide(EntityUid uid, SupermatterIntegrityComponent component, ref StartCollideEvent args)
        {
            var other = args.OtherEntity;
            if (_tagSystem.HasTag(other, component.HealTag))
            {
                component.Integrity = MathF.Min(component.MaxIntegrity, component.Integrity + component.EmitterHealAmount);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var enumerator = EntityQueryEnumerator<SupermatterIntegrityComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var comp, out var transComp))
            {
                ProcessSupermatterUpdate(uid, comp, transComp, frameTime);
            }
        }

        private void ProcessSupermatterUpdate(EntityUid uid, SupermatterIntegrityComponent comp, TransformComponent transComp, float frameTime)
        {
            var gas = _atmosphereSystem.GetContainingMixture((uid, transComp), true);

            var badConditions = false;
            if (gas != null)
            {
                if (gas.Temperature > comp.UpperTempThreshold ||
                    gas.Temperature < comp.LowerTempThreshold ||
                    gas.Pressure > comp.UpperPressureThreshold)
                {
                    badConditions = true;
                }
            }

            var integrityPercent = comp.Integrity / comp.MaxIntegrity * 100;

            // Сброс флагов предупреждений
            foreach (var key in comp.IntegrityFlags.Keys.ToList().Where(key => integrityPercent > key && comp.IntegrityFlags[key]))
            {
                comp.IntegrityFlags[key] = false;
            }

            // Выдача предупреждений по порогам
            foreach (var threshold in comp.IntegrityWarnings.Keys.OrderByDescending(t => t))
            {
                if (integrityPercent > threshold)
                    continue;

                if (!comp.IntegrityFlags.TryGetValue(threshold, out var triggered) || triggered)
                    continue;

                if (!comp.IntegrityWarnings.TryGetValue(threshold, out var msgKey))
                    continue;

                var msg = Loc.GetString(msgKey);

                SendSupermatterRadio(uid, msg, comp);
                _chatSystem.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);

                var integrityThresholds = comp.IntegrityDescription.Keys.OrderByDescending(k => k).ToArray();

                if (threshold <= integrityThresholds[4]) // 10f
                {
                    var station = _stationSystem.GetOwningStation(uid);
                    if (station != null)
                    {
                        _alertLevelSystem.SetLevel(station.Value, "yellow", true, true, true);
                        _chatSystem.DispatchStationAnnouncement(
                            station.Value,
                            Loc.GetString("supermatter-station-critical"),
                            playDefaultSound: true,
                            colorOverride: Color.Yellow
                        );
                    }
                }

                comp.IntegrityFlags[threshold] = true;
                break;
            }

            // Обработка катастрофы
            if (!comp.CatastropheActive && comp.Integrity <= comp.CatastropheThreshold)
            {
                comp.CatastropheActive = true;
                comp.CatastropheTimer = TimeSpan.FromSeconds(120); // 2 минуты
            }

            if (comp.CatastropheActive)
            {
                comp.CatastropheTimer -= TimeSpan.FromSeconds(frameTime);
                if (comp.CatastropheTimer <= TimeSpan.Zero)
                {
                    // Взрыв
                    if (TryComp(uid, out TransformComponent? transCompCat))
                    {
                        var coords = _transformSystem.ToMapCoordinates(transCompCat.Coordinates);
                        _explosionSystem.QueueExplosion(
                            coords,
                            "Default", // TODO: Отдельный прототип взрыва
                            20000f,      // totalIntensity
                            1f,         // slope
                            70f,        // maxTileIntensity
                            cause: uid
                        );
                    }
                    EntityManager.QueueDeleteEntity(uid);

                    return;
                }
            }

            // Обработка урона от плохих условий
            if (!badConditions)
                return;

            comp.TickAccumulator += TimeSpan.FromSeconds(frameTime);
            while (comp.TickAccumulator >= comp.DamageTickInterval)
            {
                comp.TickAccumulator -= comp.DamageTickInterval;

                var tickAmount = 0f;
                foreach (var v in comp.TickDamage.DamageDict.Values)
                {
                    tickAmount += (float)v;
                }

                comp.Integrity = MathF.Max(0, comp.Integrity - tickAmount);
            }
        }

        // Отправка сообщения в общую рацию от имени суперматерии
        private void SendSupermatterRadio(EntityUid source, string message, SupermatterIntegrityComponent component)
        {
            _radioSystem.SendRadioMessage(source, message, component.RadioChannel, source);
        }
    }
}
