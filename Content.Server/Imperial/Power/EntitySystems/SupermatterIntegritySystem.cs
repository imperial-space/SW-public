using Content.Server.Imperial.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.Examine;
using Robust.Shared.Utility;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Robust.Shared.Physics.Events;
using Content.Server.Popups;
using Content.Server.Chat.Systems;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Server.Lightning;
using Robust.Shared.Random;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using System;
using Content.Shared.Mobs.Components;
using Content.Server.Explosion.EntitySystems;
using Robust.Shared.GameObjects;
using Content.Shared.Damage;
using Robust.Shared.Localization;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterIntegritySystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmos = default!;
        [Dependency] private readonly RadioSystem _radio = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly SharedTransformSystem _xforms = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterIntegrityComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<SupermatterIntegrityComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SupermatterIntegrityComponent, StartCollideEvent>(OnStartCollide);
            SubscribeLocalEvent<SupermatterIntegrityComponent, ProjectileHitEvent>(OnProjectileHit);
        }

        private void SyncDamageable(EntityUid uid, SupermatterIntegrityComponent comp)
        {
            if (EntityManager.TryGetComponent<DamageableComponent>(uid, out var dmg))
            {
                var newTotal = comp.MaxIntegrity - comp.Integrity;
                var diff = newTotal - (float)dmg.TotalDamage;
                if (Math.Abs(diff) > 0.01f)
                {
                    var spec = new DamageSpecifier();
                    foreach (var type in dmg.Damage.DamageDict.Keys)
                        spec.DamageDict[type] = diff;
                    _damageable.TryChangeDamage(uid, spec, false, true, origin: null);
                }
            }
        }

        private void OnInit(EntityUid uid, SupermatterIntegrityComponent comp, ComponentInit args)
        {
            SyncDamageable(uid, comp);
        }

        private void OnProjectileHit(EntityUid uid, SupermatterIntegrityComponent comp, ref ProjectileHitEvent args)
        {
            comp.Integrity = MathF.Max(0, comp.Integrity - 10f); // 10 урона за выстрел
            SyncDamageable(uid, comp);
        }

        private void OnExamined(EntityUid uid, SupermatterIntegrityComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            var percent = component.Integrity / MathF.Max(1f, component.MaxIntegrity);
            LocId descId = default;
            foreach (var pair in component.IntegrityDescriptions.OrderByDescending(p => p.Key))
            {
                if (percent > pair.Key)
                {
                    descId = pair.Value;
                    break;
                }
            }
            if (descId == default)
                descId = "supermatter-desc-critical";
            var desc = Loc.GetString(descId);
            args.PushMarkup(desc);
        }

        private void OnStartCollide(EntityUid uid, SupermatterIntegrityComponent component, ref StartCollideEvent args)
        {
            var other = args.OtherEntity;
            if (_tagSystem.HasTag(other, component.HealTag))
            {
                var heal = 0.5f;
                component.Integrity = MathF.Min(component.MaxIntegrity, component.Integrity + heal);
                SyncDamageable(uid, component);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var enumerator = EntityQueryEnumerator<SupermatterIntegrityComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var comp, out var xform))
            {
                var gas = _atmos.GetContainingMixture((uid, xform), true, false);
                bool bad = false;
                if (gas != null)
                {
                    if (gas.Temperature > 350f || gas.Temperature < 250f || gas.Pressure > 300f)
                        bad = true;
                }

                var percent = comp.Integrity / MathF.Max(1f, comp.MaxIntegrity);
                // Сброс флагов предупреждений, если целостность снова поднялась выше порога
                foreach (var key in comp.WarningFlags.Keys.ToList())
                {
                    if (percent > key && comp.WarningFlags[key])
                        comp.WarningFlags[key] = false;
                }

                // Выдача предупреждений по порогам
                foreach (var pair in comp.WarningFlags.OrderByDescending(p => p.Key))
                {
                    var key = pair.Key;
                    if (percent < key && !comp.WarningFlags[key])
                    {
                        var msgKey = key switch
                        {
                            0.10f => "supermatter-warn-10",
                            0.25f => "supermatter-warn-25",
                            0.5f => "supermatter-warn-50",
                            0.75f => "supermatter-warn-75",
                            0.9f => "supermatter-warn-90",
                            _ => null
                        };
                        if (msgKey != null)
                        {
                            var msg = Loc.GetString(msgKey);
                            SendSupermatterRadio(uid, msg);
                            _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                            if (key == 0.10f)
                            {
                                var station = _stationSystem.GetOwningStation(uid);
                                if (station != null)
                                {
                                    _chat.DispatchStationAnnouncement(station.Value, Loc.GetString("supermatter-station-critical"), playDefaultSound: true, colorOverride: Color.Yellow);
                                    _alertLevelSystem.SetLevel(station.Value, "yellow", true, true, true);
                                }
                            }
                            comp.WarningFlags[key] = true;
                            break;
                    }
                    }
                }

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
                        if (TryComp(uid, out TransformComponent? xformCat))
                        {
                            var coords = _xforms.ToMapCoordinates(xformCat.Coordinates);
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
                    continue;
                    }
                }
                // else
                // {
                //     comp.CatastropheLightningCooldown = TimeSpan.Zero;
                // }

                if (bad)
                {
                    comp.TickAccumulator += TimeSpan.FromSeconds(frameTime);
                    while (comp.TickAccumulator >= comp.TickInterval)
                    {
                        comp.TickAccumulator -= comp.TickInterval;
                        // Прямое уменьшение Integrity
                        var tickAmount = 0f;
                        foreach (var v in comp.TickDamage.DamageDict.Values)
                            tickAmount += (float)v;
                        comp.Integrity = MathF.Max(0, comp.Integrity - tickAmount);
                        SyncDamageable(uid, comp);
                    }
                }
            }
        }

        // Отправка сообщения в общую рацию от имени суперматерии
        public void SendSupermatterRadio(EntityUid source, string message)
        {
            var channel = _proto.Index<RadioChannelPrototype>("Engineering");
            _radio.SendRadioMessage(source, message, channel, source);
        }
    }
}

