using Content.Shared.Imperial.PinpointerCritical.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Content.Server.Pinpointer;

namespace Content.Server.Imperial.PinpointerCritical.Systems;

public sealed partial class PinpointerCriticalSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PinpointerCriticalComponent, ActivateInWorldEvent>(OnSwitch);
    }
    public void OnSwitch(EntityUid uid, PinpointerCriticalComponent comp, ActivateInWorldEvent ev)
    {
        var userCoords = _transform.GetMapCoordinates(uid); // Координаты игрока
        EntityUid? closestUid = null;
        var closestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<MobThresholdsComponent>();
        while (query.MoveNext(out var uidd, out var compp))
        {
            if (compp.CurrentThresholdState == MobState.Critical) // Проверка на критическое состояние
            {
                var targetCoords = _transform.GetMapCoordinates(uidd); // Координаты цели
                var distance = (float)(targetCoords.Position - userCoords.Position).LengthSquared(); // Квадрат расстояния

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestUid = uidd;
                }

            }
        }

        if (closestUid != null)
        {
            Log.Info($"Ближайший критический моб: {ToPrettyString(closestUid)} на расстоянии {MathF.Sqrt(closestDistance)}: вызвано {ToPrettyString(uid)}");
        }
        else
        {
            Log.Info($"Критических мобов не найдено: вызвано {ToPrettyString(uid)}");
        }
        if (TryComp<PinpointerComponent>(ev.Target, out var pinComp) && closestUid != null)
        {
            _pinpointer.SetTarget(uid, closestUid, pinComp);
        }
    }

}
