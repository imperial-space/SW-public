using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

// TODO: move all magical numbers to const cuz im too lazy to do it rn
public sealed class SeaWindOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<MapId, List<WindParticle>> _particlesByMap = new();
    private readonly List<MapId> _mapsToRemove = new();

    private float _windAngle;
    private float _windStrength;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SeaWindOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 100;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var targetAngle = _configuration.GetCVar(ShipsCCVars.WindRotation) * MathF.PI / 180f;
        var rawStrength = MathF.Max(
            MathF.Max(0f, _configuration.GetCVar(ShipsCCVars.StormLevel)),
            MathF.Max(0f, _configuration.GetCVar(ShipsCCVars.WindPower)));

        if (rawStrength <= 0.01f)
            rawStrength = 0;

        var targetStrength = Math.Clamp(rawStrength / 10f, 0f, 1f);

        var angleDelta = Normalize(targetAngle - _windAngle);

        _windAngle = Normalize(_windAngle + angleDelta * MathF.Min(1f, args.DeltaSeconds * 4f));
        _windStrength += (targetStrength - _windStrength) * MathF.Min(1f, args.DeltaSeconds * 2f);

        UpdateParticles(args.DeltaSeconds);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) || sea.Disabled)
            return false;

        if (_configuration.GetCVar(ShipsCCVars.WindEnabled) && _windStrength > 0.01f)
            return true;

        return _particlesByMap.TryGetValue(args.MapId, out var particles) && particles.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var particles = AllParticles(args.MapId);
        var visibleBounds = args.WorldAABB;

        if (_configuration.GetCVar(ShipsCCVars.WindEnabled) && _windStrength > 0.01f)
            SpawnParticles(particles, visibleBounds);

        DrawParticles(args.WorldHandle, particles, visibleBounds);
    }

    private List<WindParticle> AllParticles(MapId mapId)
    {
        if (_particlesByMap.TryGetValue(mapId, out var particles))
            return particles;

        particles = new List<WindParticle>();
        _particlesByMap[mapId] = particles;
        return particles;
    }

    private void UpdateParticles(float frameTime)
    {
        _mapsToRemove.Clear();

        foreach (var (mapId, particles) in _particlesByMap)
        {
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.Age += frameTime;
                particle.SpawnAge += frameTime;
                var moveRatio = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
                var curveDrift = MathF.Sin(moveRatio * MathF.PI);
                particle.Position += (particle.Velocity + particle.CurveVelocity * curveDrift) * frameTime;

                if (particle.Age >= particle.Lifetime)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particles[i] = particle;
            }

            if (particles.Count == 0)
                _mapsToRemove.Add(mapId);
        }

        foreach (var mapId in _mapsToRemove)
        {
            _particlesByMap.Remove(mapId);
        }
    }

    private void SpawnParticles(List<WindParticle> particles, Box2 visibleBounds)
    {
        var area = visibleBounds.Width * visibleBounds.Height;
        var density = 0.052f + _windStrength * 0.09f;
        var targetCount = Math.Clamp((int) MathF.Ceiling(area * density), 34, 220);
        var activeBounds = visibleBounds.Enlarged(1.8f + _windStrength * 2.8f);

        var activeCount = particles.Count(particle => activeBounds.Contains(particle.Position));

        var missingParticles = targetCount - activeCount;
        for (var i = 0; i < missingParticles; i++)
        {
            particles.Add(CreateParticle(visibleBounds));
        }
    }

    private WindParticle CreateParticle(Box2 visibleBounds)
    {
        var angle = new Angle(_windAngle + _random.NextFloat(-0.2f, 0.2f));
        var direction = angle.ToVec();
        var perpendicular = new Vector2(-direction.Y, direction.X);

        // kill me pls
        var speed = _random.NextFloat(3.4f, 5.8f) + _windStrength * _random.NextFloat(4.8f, 8.8f);
        var travelDistance = _random.NextFloat(20f, 28f) + _windStrength * _random.NextFloat(18f, 32f);
        var lifetime = travelDistance / MathF.Max(speed, 0.1f);
        var length = _random.NextFloat(0.95f, 1.55f) + _windStrength * _random.NextFloat(0.55f, 1.35f);

        var halfWidth = visibleBounds.Width * 0.5f;
        var halfHeight = visibleBounds.Height * 0.5f;

        var directionExtent = MathF.Abs(direction.X) * halfWidth + MathF.Abs(direction.Y) * halfHeight;
        var lateralExtent = MathF.Abs(perpendicular.X) * halfWidth + MathF.Abs(perpendicular.Y) * halfHeight;
        var spawnMargin = 4f + _windStrength * 6f;

        var position =
            visibleBounds.Center
            - direction * (directionExtent + _random.NextFloat(0.5f, spawnMargin))
            + perpendicular * _random.NextFloat(-lateralExtent - 2f, lateralExtent + 2f)
            + direction * _random.NextFloat(-1f, 1f);

        var particle = new WindParticle
        {
            Position = position,
            Velocity = direction * speed,
            Direction = direction,
            Lifetime = lifetime,
            Length = length,
            Curve = _random.NextFloat(-0.14f, 0.14f) * length,
            CurveBias = _random.NextFloat(-0.2f, 0.2f) * length,
            Alpha = _random.NextFloat(0.45f, 0.95f),
            WidthScale = _random.NextFloat(0.9f, 1.22f),
        };
        particle.CurveVelocity = perpendicular * (particle.Curve / MathF.Max(lifetime, 0.1f)) * 1.35f;

        var visibleTravelTime = (directionExtent * 1.45f + spawnMargin) / MathF.Max(speed, 0.1f);
        particle.Age = _random.NextFloat(0f, MathF.Min(particle.Lifetime * 0.65f, visibleTravelTime * 0.8f));
        particle.Position += particle.Velocity * particle.Age;

        var moveRatio = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
        var curveIntegral = (particle.Lifetime / MathF.PI) * (1f - MathF.Cos(moveRatio * MathF.PI));
        particle.Position += particle.CurveVelocity * curveIntegral;

        return particle;
    }

    private void DrawParticles(DrawingHandleWorld handle, List<WindParticle> particles, Box2 visibleBounds)
    {
        var drawBounds = visibleBounds.Enlarged((1.8f + _windStrength * 2.8f) + 2f);

        foreach (var particle in particles)
        {
            if (!drawBounds.Contains(particle.Position))
                continue;

            var lifeRatio = particle.Age / particle.Lifetime;
            var fadeIn = SmoothStep(0f, 0.18f, lifeRatio);
            var fadeOut = 1f - SmoothStep(0.82f, 1f, lifeRatio);
            var strengthAlpha = 0.18f + _windStrength * 0.4f;

            var edgeFade = 1f;
            if (!visibleBounds.Contains(particle.Position))
            {
                var outerDistance = MathF.Min(
                    MathF.Min(particle.Position.X - drawBounds.Left, drawBounds.Right - particle.Position.X),
                    MathF.Min(particle.Position.Y - drawBounds.Bottom, drawBounds.Top - particle.Position.Y));

                var innerDistance = MathF.Max(
                    MathF.Max(visibleBounds.Left - particle.Position.X, particle.Position.X - drawBounds.Right),
                    MathF.Max(visibleBounds.Bottom - particle.Position.Y, particle.Position.Y - drawBounds.Top));

                var fadeWidth = MathF.Max(outerDistance + innerDistance, 0.001f);
                edgeFade = SmoothStep(0f, fadeWidth, outerDistance);
            }
            var spawnFade = SmoothStep(0f, 0.28f, particle.SpawnAge);

            var alpha = particle.Alpha * strengthAlpha * fadeIn * fadeOut * edgeFade * spawnFade;

            if (alpha <= 0.001f)
                continue;

            var curve = GetCurvePointsFromParticle(particle);
            DrawRibbon(handle, curve, particle, Color.White.WithAlpha(alpha));
        }
    }

    private static Vector2[] GetCurvePointsFromParticle(WindParticle particle)
    {
        var halfLength = particle.Length * 0.5f;
        var perpendicular = new Vector2(-particle.Direction.Y, particle.Direction.X);

        var start = particle.Position - particle.Direction * halfLength;
        var end = particle.Position + particle.Direction * halfLength;
        var control = particle.Position + particle.Direction * particle.CurveBias + perpendicular * particle.Curve;
        var points = new Vector2[10];

        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (points.Length - 1f);
            var invT = 1f - t;
            // i hate this so much
            points[i] = invT*invT * start + 2f * invT * t * control + t*t * end;
        }

        return points;
    }

    private float RibbonHWidth(WindParticle particle, float pointT)
    {
        var centerWeight = SmoothStep(0f, 1f, 1f - MathF.Abs(pointT - 0.5f) * 2f);
        centerWeight *= centerWeight;
        return (0.004f + _windStrength * 0.0032f + centerWeight * 0.0064f) * particle.WidthScale;
    }

    private void DrawRibbon(DrawingHandleWorld handle, Vector2[] curve, WindParticle particle, Color color)
    {
        var vertices = new Vector2[(curve.Length - 1) * 6];
        var vertexIndex = 0;

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var startT = i / (float)(curve.Length - 1);
            var endT = (i + 1) / (float)(curve.Length - 1);

            var startNormal = RibbonNormal(curve, i);
            var endNormal = RibbonNormal(curve, i + 1);

            var startHalfWidth = RibbonHWidth(particle, startT);
            var endHalfWidth = RibbonHWidth(particle, endT);

            var startLeft = curve[i] - startNormal * startHalfWidth;
            var startRight = curve[i] + startNormal * startHalfWidth;
            var endLeft = curve[i + 1] - endNormal * endHalfWidth;
            var endRight = curve[i + 1] + endNormal * endHalfWidth;

            vertices[vertexIndex++] = startLeft;
            vertices[vertexIndex++] = startRight;
            vertices[vertexIndex++] = endRight;
            vertices[vertexIndex++] = startLeft;
            vertices[vertexIndex++] = endRight;
            vertices[vertexIndex++] = endLeft;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private static Vector2 RibbonNormal(Vector2[] curve, int index)
    {
        Vector2 tangent;
        if (index == 0)
            tangent = curve[1] - curve[0];
        else if (index == curve.Length - 1)
            tangent = curve[index] - curve[index - 1];
        else
            tangent = curve[index + 1] - curve[index - 1];

        return tangent.LengthSquared() <= 0.0001f
            ? Vector2.UnitY
            : Vector2.Normalize(new Vector2(-tangent.Y, tangent.X));
    }

    #region Helpers
    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Normalize(float angle)
    {
        const float fullRotation = MathF.PI * 2f;

        while (angle > MathF.PI)
        {
            angle -= fullRotation;
        }

        while (angle < -MathF.PI)
        {
            angle += fullRotation;
        }

        return angle;
    }
    #endregion

    private sealed class WindParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 CurveVelocity;
        public Vector2 Direction;
        public float Age;
        public float SpawnAge;
        public float Lifetime;
        public float Length;
        public float Curve;
        public float CurveBias;
        public float Alpha;
        public float WidthScale;
    }
}
