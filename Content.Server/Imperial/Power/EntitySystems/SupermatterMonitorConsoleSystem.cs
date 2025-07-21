using Content.Server.Imperial.Power.Components;
using Content.Server.Imperial.Power.Components;
using Content.Shared.Examine;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using System.Linq;
using Robust.Shared.GameObjects;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

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
            SupermatterIntegrityComponent? nearest = null;
            float minDist = float.MaxValue;
            foreach (var sm in EntityManager.EntityQuery<SupermatterIntegrityComponent>())
            {
                var smXform = Transform(sm.Owner);
                if (smXform.MapID != mapId)
                    continue;
                var smPos = _xforms.GetMapCoordinates(smXform).Position;
                var dist = (smPos - pos).LengthSquared();
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = sm;
                }
            }

            if (nearest != null)
            {
                // Цвет по уровню прочности
                float percent = nearest.Integrity / MathF.Max(1f, nearest.MaxIntegrity);
                string color = percent > 0.75f ? "green" : percent > 0.25f ? "yellow" : "red";
                args.PushMarkup($"[color={color}]Целостность ближайшей суперматерии: {nearest.Integrity:0.##} / {nearest.MaxIntegrity:0.##}[/color]\n");
                // Пиликанье при низкой прочности
                if (percent <= 0.25f)
                {
                    _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/beep.ogg"), uid);
                }
                // Получаем атмосферу вокруг кристалла
                var smXform = Transform(nearest.Owner);
                var gas = _atmos.GetContainingMixture((nearest.Owner, smXform));
                if (gas != null)
                {
                    args.PushMarkup($"[color=yellow]Атмосфера: давление {gas.Pressure:0.0} кПа, температура {gas.Temperature:0.0} K[/color]\n");
                }
                // Примерное время до следующего всплеска
                if (EntityManager.TryGetComponent<SupermatterEventComponent>(nearest.Owner, out var events))
                {
                    float next = events.NextEventTimer;
                float approx = MathF.Max(0, next + _random.Next(-60, 61));
                int minutes = (int)MathF.Round(approx / 60f);
                args.PushMarkup($"[color=yellow]До следующего энергетического всплеска: ~{minutes} мин.[/color]\n");
                }
            }
            else
            {
                args.PushMarkup("[color=gray]Поблизости не найдено кристаллов суперматерии.[/color]");
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var (console, xform) in EntityQuery<SupermatterMonitorConsoleComponent, TransformComponent>())
            {
                // Найти ближайший кристалл суперматерии
                var mapId = xform.MapID;
                var pos = _xforms.GetMapCoordinates(xform).Position;
                SupermatterIntegrityComponent? nearest = null;
                float minDist = float.MaxValue;
                foreach (var sm in EntityManager.EntityQuery<SupermatterIntegrityComponent>())
                {
                    var smXform = Transform(sm.Owner);
                    if (smXform.MapID != mapId)
                        continue;
                    var smPos = _xforms.GetMapCoordinates(smXform).Position;
                    var dist = (smPos - pos).LengthSquared();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = sm;
                    }
                }
                // Если есть опасная суперматерия — пиликаем раз в 2 секунды
                if (nearest != null)
                {
                    float percent = nearest.Integrity / MathF.Max(1f, nearest.MaxIntegrity);
                    if (percent <= 0.25f)
                    {
                        console.BeepCooldownTimer -= frameTime;
                        if (console.BeepCooldownTimer <= 0f)
                        {
                            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/beep.ogg"), console.Owner);
                            console.BeepCooldownTimer = 2f;
                        }
                    }
                    else
                    {
                        console.BeepCooldownTimer = 0f;
                    }
                }
                else
                {
                    console.BeepCooldownTimer = 0f;
                }
            }
        }
    }
}
