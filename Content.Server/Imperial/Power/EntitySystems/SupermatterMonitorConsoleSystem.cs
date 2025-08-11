using Content.Server.Imperial.Power.Components;
using Content.Shared.Examine;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterMonitorConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly AtmosphereSystem _atmosSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterMonitorConsoleComponent, ExaminedEvent>(OnExamined);
        }

        private void OnExamined(EntityUid uid, SupermatterMonitorConsoleComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            // Найти ближайший кристалл суперматерии
            var nearestUid = FindNearestSupermatter(uid);
            if (nearestUid == null)
                args.PushMarkup(Loc.GetString("supermatter-monitor-none-nearby"));

            if (!TryComp(nearestUid, out SupermatterIntegrityComponent? nearest))
                return;

            var integrity = (int)Math.Round(nearest.Integrity / nearest.MaxIntegrity * 100);
            var integrityThresholds = nearest.IntegrityDescription.Keys.OrderByDescending(k => k).ToArray();

            // Цвет по уровню прочности
            var color = integrity switch
            {
                _ when integrity > integrityThresholds[0] => Color.Green, // >95
                _ when integrity > integrityThresholds[1] => Color.Yellow, // >75
                _ when integrity > integrityThresholds[2] => Color.Orange, // >50
                _ when integrity > integrityThresholds[3] => Color.Brown, // >25
                _ when integrity > integrityThresholds[4] => Color.DarkRed, // >10
                _ => Color.Red,
            };

            args.PushMarkup(Loc.GetString("supermatter-monitor-integrity",
                ("integrity", integrity),
                ("color", color)));

            // Пиликанье при низкой прочности
            if (integrity <= integrityThresholds[3])
                _audioSystem.PlayPvs(component.BeepSound, uid);

            // Получаем атмосферу вокруг кристалла
            var transComp = Transform(nearestUid.Value);
            var gas = _atmosSystem.GetContainingMixture((nearestUid.Value, transComp));

            if (gas == null)
                return;

            var pressure = (int)Math.Round(gas.Pressure);
            var temperature = (int)Math.Round(gas.Temperature);
            args.PushMarkup(Loc.GetString("supermatter-monitor-atmospherics",
                ("pressure", pressure),
                ("temperature", temperature)));

            // Примерное время до следующего всплеска
            if (!EntityManager.TryGetComponent<SupermatterEventComponent>(nearestUid.Value, out var events))
                return;

            var next = events.NextEventTimer.TotalSeconds;
            var approx = Math.Max(0, next + _random.Next(-60, 61));
            var minutes = (int)Math.Round(approx / 60.0);
            args.PushMarkup(Loc.GetString("supermatter-monitor-next-event", ("minutes", minutes)));
        }

        private EntityUid? FindNearestSupermatter(EntityUid consoleUid)
        {
            var transformCompConsole = Transform(consoleUid);
            var mapId = transformCompConsole.MapID;
            var pos = _transformSystem.GetMapCoordinates(transformCompConsole).Position;
            EntityUid? nearest = null;
            var minDist = float.MaxValue;
            var smEnumerator = EntityQueryEnumerator<SupermatterIntegrityComponent, TransformComponent>();
            while (smEnumerator.MoveNext(out var smUid, out _, out var transComp))
            {
                if (transComp.MapID != mapId)
                    continue;

                var smPos = _transformSystem.GetMapCoordinates(smUid).Position;
                var dist = (smPos - pos).LengthSquared();
                if (dist > minDist)
                    continue;

                minDist = dist;
                nearest = smUid;
            }
            return nearest;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var enumerator = EntityQueryEnumerator<SupermatterMonitorConsoleComponent, TransformComponent>();
            while (enumerator.MoveNext(out var uid, out var console, out _))
            {
                // Найти ближайший кристалл суперматерии
                var nearestUid = FindNearestSupermatter(uid);
                if (nearestUid == null ||
                    !EntityManager.TryGetComponent<SupermatterIntegrityComponent>(nearestUid.Value, out var nearest))
                {
                    console.BeepCooldownTimer = TimeSpan.Zero;
                }
                else
                {
                    var integrity = nearest.Integrity / nearest.MaxIntegrity * 100;
                    var integrityThresholds = nearest.IntegrityDescription.Keys.OrderByDescending(k => k).ToArray();

                    if (integrity >= integrityThresholds[3])
                    {
                        console.BeepCooldownTimer = TimeSpan.Zero;
                    }
                    else
                    {
                        console.BeepCooldownTimer -= TimeSpan.FromSeconds(frameTime);
                        if (console.BeepCooldownTimer > TimeSpan.Zero)
                            continue;

                        _audioSystem.PlayPvs(console.BeepSound, uid);
                        console.BeepCooldownTimer = TimeSpan.FromSeconds(2);
                    }
                }
            }
        }
    }
}
