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
using Content.Server.Radiation.Systems;
using Robust.Shared.Random;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using System;
using Content.Shared.Mobs.Components;
using Content.Server.Explosion.EntitySystems;
using Robust.Shared.GameObjects;

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
        [Dependency] private readonly LightningSystem _lightning = default!;
        [Dependency] private readonly RadiationSystem _radiation = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly IGameTiming Timing = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly SharedTransformSystem _xforms = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterIntegrityComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<SupermatterIntegrityComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SupermatterIntegrityComponent, StartCollideEvent>(OnStartCollide);
        }

        private void OnInit(EntityUid uid, SupermatterIntegrityComponent comp, ComponentInit args)
        {
            // 
        }

        private void OnExamined(EntityUid uid, SupermatterIntegrityComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            var percent = component.Integrity / MathF.Max(1f, component.MaxIntegrity);
            string desc = "";
            if (percent > 0.95f)
                desc = "Кристалл выглядит абсолютно целым.";
            else if (percent > 0.75f)
                desc = "На кристалле видны небольшие царапины.";
            else if (percent > 0.5f)
                desc = "На кристалле появились трещины.";
            else if (percent > 0.25f)
                desc = "Кристалл покрыт множеством трещин и выглядит нестабильно.";
            else
                desc = "Кристалл вот-вот разрушится! Он светится и вибрирует.";

            args.PushMarkup(desc);
        }

        private void OnStartCollide(EntityUid uid, SupermatterIntegrityComponent component, ref StartCollideEvent args)
        {
            var other = args.OtherEntity;
            if (_tagSystem.HasTag(other, "EmitterBolt"))
            {
                var heal = 0.5f;
                component.Integrity = MathF.Min(component.MaxIntegrity, component.Integrity + heal);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var (comp, xform) in EntityQuery<SupermatterIntegrityComponent, TransformComponent>())
            {
                var uid = xform.Owner;
                var gas = _atmos.GetContainingMixture((uid, xform), true, false);
                bool bad = false;
                if (gas != null)
                {
                    if (gas.Temperature > 350f || gas.Temperature < 250f || gas.Pressure > 300f)
                        bad = true;
                }

                var percent = comp.Integrity / MathF.Max(1f, comp.MaxIntegrity);
                if (percent > 0.9f && comp._warned90)
                    comp._warned90 = false;
                if (percent > 0.75f && comp._warned75)
                    comp._warned75 = false;
                if (percent > 0.5f && comp._warned50)
                    comp._warned50 = false;
                if (percent > 0.25f && comp._warned25)
                    comp._warned25 = false;
                if (percent > 0.10f && comp._warned10)
                    comp._warned10 = false;

                if (percent < 0.10f && !comp._warned10)
                {
                    var msg = "!!! СУПЕРМАТЕРИЯ НА ГРАНИ РАЗРУШЕНИЯ !!!";
                    SendSupermatterRadio(uid, msg);
                    _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                    var station = _stationSystem.GetOwningStation(uid);
                    if (station != null)
                    {
                        _chat.DispatchStationAnnouncement(station.Value, "ВНИМАНИЕ! Суперматерия на грани разрушения! Немедленно стабилизируйте кристалл!", playDefaultSound: true, colorOverride: Color.Yellow);
                        _alertLevelSystem.SetLevel(station.Value, "yellow", true, true, true);
                    }
                    comp._warned10 = true;
                }
                else if (percent < 0.25f && !comp._warned25)
                {
                    var msg = "ВНИМАНИЕ! Целостность суперматерии критически низкая! Немедленно стабилизируйте кристалл!";
                    SendSupermatterRadio(uid, msg);
                    _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                    comp._warned25 = true;
                }
                else if (percent < 0.5f && !comp._warned50)
                {
                    var msg = "Внимание! Суперматерия сильно повреждена. Ситуация опасна.";
                    SendSupermatterRadio(uid, msg);
                    _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                    comp._warned50 = true;
                }
                else if (percent < 0.75f && !comp._warned75)
                {
                    var msg = "Суперматерия получила повреждения. Рекомендуется проверить состояние кристалла.";
                    SendSupermatterRadio(uid, msg);
                    _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                    comp._warned75 = true;
                }
                else if (percent < 0.9f && !comp._warned90)
                {
                    var msg = "Зарегистрированы незначительные повреждения суперматерии.";
                    SendSupermatterRadio(uid, msg);
                    _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, ChatTransmitRange.Normal);
                    comp._warned90 = true;
                }

                if (comp.Integrity <= comp.CatastropheThreshold)
                {
                    if (!comp.CatastropheActive)
                    {
                        // Запуск катастрофы
                        comp.CatastropheActive = true;
                        comp.CatastropheTimer = 120f; // 2 минуты
                        AnnounceFromConsole(uid, "!!! СУПЕРМАТЕРИЯ КРИТИЧЕСКИ НЕСТАБИЛЬНА: УНИЧТОЖИТЕЛЬНЫЙ ВЫПУСК ЭНЕРГИИ !!!");
                    }
                }
                // Катастрофа
                if (comp.CatastropheActive)
                {
                    comp.CatastropheTimer -= frameTime;

                    if (comp.CatastropheTimer <= 0f)
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
                else
                {
                    comp.CatastropheLightningCooldown = 0f;
                    // if (comp.Integrity <= comp.CatastropheThreshold)
                    // {
                    //     EntityManager.QueueDeleteEntity(uid);
                    //     continue;
                    // }
                }

                if (bad)
                {
                    comp.Integrity = MathF.Max(comp.CatastropheThreshold, comp.Integrity - comp.TickDamage * frameTime);
                }
            }
        }

        // Отправка сообщения в общую рацию от имени суперматерии
        private void SendSupermatterRadio(EntityUid source, string message)
        {
            var channel = _proto.Index<RadioChannelPrototype>("Common");
            _radio.SendRadioMessage(source, message, channel, source);
        }

        private void AnnounceFromConsole(EntityUid crystal, string message)
        {
            // Найти ближайшую консоль мониторинга
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
            // Сообщение в инженерный канал
            _radio.SendRadioMessage(nearestConsole ?? crystal, message, "Engineering", nearestConsole ?? crystal);
            // Локальный чат рядом с кристаллом
            _chat.TrySendInGameICMessage(nearestConsole ?? crystal, message, InGameICChatType.Speak, false);
        }
    }
}
