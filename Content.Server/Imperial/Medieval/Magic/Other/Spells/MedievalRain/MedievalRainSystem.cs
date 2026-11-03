using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalRain;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalRain;


public sealed class MedievalRainSystem : EntitySystem
{
    private const float GoldenAngle = 2.39996f;
    /// <summary>
    /// Time the marked circle is shown before the first icicle starts to fall
    /// </summary>
    private const float FirstDropDelay = 0.8f;
    private const float MaxCastDistance = 14f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalRainComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalRainComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Pending.Count == 0) continue;

            var center = _transform.GetMapCoordinates(uid);

            for (var i = component.Pending.Count - 1; i >= 0; i--)
            {
                var (at, offset) = component.Pending[i];

                if (_timing.CurTime < at) continue;

                component.Pending.RemoveAt(i);

                var drop = Spawn(component.Drop, new MapCoordinates(center.Position + offset, center.MapId));

                if (TryComp<MedievalRainDropComponent>(drop, out var dropComponent))
                    dropComponent.Performer = component.Performer;
            }

            if (component.Pending.Count == 0) QueueDel(uid);
        }
    }

    private void OnSpawn(EntityUid uid, MedievalRainComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        // The action pipeline does not reject out-of-range world targets, so the distance is checked here
        var rainXform = Transform(uid);

        if (!TryComp(args.Performer, out TransformComponent? performerXform)
            || performerXform.MapID != rainXform.MapID
            || !IsValidDistance(_transform.GetWorldPosition(rainXform), _transform.GetWorldPosition(performerXform)))
        {
            QueueDel(uid);
            return;
        }

        component.Performer = args.Performer;

        if (component.Marker is { } marker)
            Spawn(marker, _transform.GetMapCoordinates(uid));

        // Even coverage of the circle, so the damage does not depend on a lucky roll
        var delays = new List<float>();

        for (var i = 0; i < component.Count; i++)
            delays.Add(FirstDropDelay + component.Duration * (i + _random.NextFloat()) / component.Count);

        _random.Shuffle(delays);

        for (var i = 0; i < component.Count; i++)
        {
            var angle = i * GoldenAngle + _random.NextFloat(-0.4f, 0.4f);
            var distance = component.Radius * MathF.Sqrt((i + 0.5f) / component.Count);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            component.Pending.Add((_timing.CurTime + TimeSpan.FromSeconds(delays[i]), offset));
        }
    }

    private static bool IsValidDistance(Vector2 rain, Vector2 performer)
    {
        return float.IsFinite(rain.X) && float.IsFinite(rain.Y) && (rain - performer).Length() <= MaxCastDistance;
    }
}
