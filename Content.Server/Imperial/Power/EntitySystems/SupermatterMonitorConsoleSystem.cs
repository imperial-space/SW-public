using Content.Server.Imperial.Power.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using System.Linq;
using Robust.Shared.GameObjects;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using System;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterMonitorConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _xforms = default!;
        [Dependency] private readonly AtmosphereSystem _atmos = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterMonitorConsoleComponent, ExaminedEvent>(OnExamined);
        }

        private void OnExamined(EntityUid uid, SupermatterMonitorConsoleComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            var xform = Transform(uid);
            var mapId = xform.MapID;
            var pos = _xforms.GetMapCoordinates(xform).Position;

            // Найти ближайший кристалл суперматерии
            var nearestUid = FindNearestSupermatter(uid, xform);
            if (nearestUid != null && EntityManager.TryGetComponent<SupermatterIntegrityComponent>(nearestUid.Value, out var nearest))
            {
                // Цвет по уровню прочности
                float percent = nearest.Integrity / MathF.Max(1f, nearest.MaxIntegrity);
                var color = percent > 0.75f ? "green" : percent > 0.25f ? "yellow" : "red";
                var integrityPrefix = Loc.GetString("supermatter-monitor-integrity-prefix");
                var integrityText = $"{integrityPrefix} {nearest.Integrity:0} / {nearest.MaxIntegrity:0}";
                args.PushMarkup($"[color={color}]{integrityText}[/color]\n");
                // Пиликанье при низкой прочности
                if (percent <= 0.25f)
                {
                    _audio.PlayPvs(component.BeepSound, uid);
                }
                // Получаем атмосферу вокруг кристалла
                var smXform = Transform(nearestUid.Value);
                var gas = _atmos.GetContainingMixture((nearestUid.Value, smXform));
                if (gas != null)
                {
                    var atmosPrefix = Loc.GetString("supermatter-monitor-atmos-prefix");
                    var pressure = gas.Pressure.ToString("0.0");
                    var temperature = gas.Temperature.ToString("0.0");
                    args.PushMarkup($"{atmosPrefix} pressure {pressure} kPa, temperature {temperature} K\n");
                }
                // Примерное время до следующего всплеска
                if (EntityManager.TryGetComponent<SupermatterEventComponent>(nearestUid.Value, out var events))
                {
                    var nextEventPrefix = Loc.GetString("supermatter-monitor-next-event-prefix");
                    double next = events.NextEventTimer.TotalSeconds;
                    double approx = Math.Max(0, next + _random.Next(-60, 61));
                    int minutes = (int)Math.Round(approx / 60.0);
                    args.PushMarkup($"{nextEventPrefix} {minutes} min.\n");
                }
            }
            else
            {
                args.PushMarkup(Loc.GetString("supermatter-monitor-none-nearby"));
            }
        }

        private EntityUid? FindNearestSupermatter(EntityUid consoleUid, TransformComponent xform)
        {
            var mapId = xform.MapID;
            var pos = _xforms.GetMapCoordinates(consoleUid).Position;
            EntityUid? nearest = null;
            float minDist = float.MaxValue;
            var smEnumerator = EntityQueryEnumerator<SupermatterIntegrityComponent, TransformComponent>();
            while (smEnumerator.MoveNext(out var smUid, out var sm, out var smXform))
            {
                if (smXform.MapID != mapId)
                    continue;
                var smPos = _xforms.GetMapCoordinates(smUid).Position;
                var dist = (smPos - pos).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = smUid;
                }
            }
            return nearest;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var enumerator = EntityQueryEnumerator<SupermatterMonitorConsoleComponent, TransformComponent>();
            while (enumerator.MoveNext(out var console, out var xform))
            {
                // Найти ближайший кристалл суперматерии
                var nearestUid = FindNearestSupermatter(console.Owner, xform);
                if (nearestUid != null && EntityManager.TryGetComponent<SupermatterIntegrityComponent>(nearestUid.Value, out var nearest))
                {
                    float percent = nearest.Integrity / MathF.Max(1f, nearest.MaxIntegrity);
                    if (percent <= 0.25f)
                    {
                        console.BeepCooldownTimer -= TimeSpan.FromSeconds(frameTime);
                        if (console.BeepCooldownTimer <= TimeSpan.Zero)
                        {
                            _audio.PlayPvs(console.BeepSound, console.Owner);
                            console.BeepCooldownTimer = TimeSpan.FromSeconds(2);
                        }
                    }
                    else
                    {
                        console.BeepCooldownTimer = TimeSpan.Zero;
                    }
                }
                else
                {
                    console.BeepCooldownTimer = TimeSpan.Zero;
                }
            }
        }
    }
}
