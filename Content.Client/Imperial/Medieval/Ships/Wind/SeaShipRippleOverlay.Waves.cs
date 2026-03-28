using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed partial class SeaShipRippleOverlay
{
    private void TryEmitSmallWave(MapId mapId, ShipMotionState motion)
    {
        if (motion.NextSmallWaveTime > _timing.CurTime.TotalSeconds)
            return;

        if (_emitPoints.Count == 0)
            return;

        var sample = _emitPoints[_random.Next(_emitPoints.Count)];
        var waveDirection = Rotate(sample.Direction, _random.NextFloat(-0.42f, 0.42f));

        EmitWaveParticle(
            mapId,
            sample.Position,
            waveDirection,
            0.06f + _random.NextFloat() * 0.05f,
            0.08f + _random.NextFloat() * 0.05f,
            0.2f + _random.NextFloat() * 0.08f,
            0.018f + _random.NextFloat() * 0.01f,
            0.0015f + _random.NextFloat() * 0.0008f,
            0.42f + _random.NextFloat() * 0.14f,
            0.055f + _random.NextFloat() * 0.022f,
            0.09f + _random.NextFloat() * 0.035f,
            0.58f + _random.NextFloat() * 0.2f,
            motion.WaveSeed + _random.NextFloat() * 3f,
            0.9f + _random.NextFloat() * 0.16f,
            1.04f + _random.NextFloat() * 0.08f);

        motion.NextSmallWaveTime = (float) _timing.CurTime.TotalSeconds + _random.NextFloat(SmallWaveMinDelay, SmallWaveMaxDelay);
    }

    private void TryEmitMovingWave(
        MapId mapId,
        ShipMotionState motion,
        Vector2 shipCenter)
    {
        if (motion.Speed < MovementWaveMinSpeed)
            return;

        if (motion.NextMovingWaveTime > _timing.CurTime.TotalSeconds)
            return;

        if (_emitPoints.Count == 0)
            return;

        var motionDirection = Normalize(motion.InstantWorldDirection);
        var waveDirection = -motionDirection;
        var sideDirection = new Vector2(-waveDirection.Y, waveDirection.X);
        var bestBackProjection = float.MinValue;
        RippleEmitPoint? bestPoint = null;

        foreach (var sample in _emitPoints)
        {
            var offset = sample.Position - shipCenter;
            var backProjection = Vector2.Dot(offset, waveDirection);
            var lateralProjection = MathF.Abs(Vector2.Dot(offset, sideDirection));

            if (!(backProjection > bestBackProjection + 0.001f) &&
                (!(MathF.Abs(backProjection - bestBackProjection) <= 0.001f) ||
                 !(bestPoint == null || lateralProjection < MathF.Abs(Vector2.Dot(bestPoint.Position - shipCenter, sideDirection)))))
                continue;

            bestBackProjection = backProjection;
            bestPoint = sample;
        }

        if (bestPoint == null)
            return;

        var weightedPosition = Vector2.Zero;
        var weightSum = 0f;
        var frontWindow = 0.16f;
        var sternMinLateral = float.MaxValue;
        var sternMaxLateral = float.MinValue;
        var sternSampleCount = 0;

        foreach (var sample in _emitPoints)
        {
            var offset = sample.Position - shipCenter;
            var backProjection = Vector2.Dot(offset, waveDirection);
            var signedLateral = Vector2.Dot(offset, sideDirection);
            var facing = Vector2.Dot(sample.Direction, waveDirection);

            if (facing <= 0.1f)
                continue;

            if (bestBackProjection - backProjection > frontWindow)
                continue;

            var frontWeight = 1f - Math.Clamp((bestBackProjection - backProjection) / frontWindow, 0f, 1f);
            var weight = facing * (0.35f + frontWeight * 0.65f);

            if (weight <= 0.001f)
                continue;

            sternMinLateral = MathF.Min(sternMinLateral, signedLateral);
            sternMaxLateral = MathF.Max(sternMaxLateral, signedLateral);
            sternSampleCount++;
            weightedPosition += sample.Position * weight;
            weightSum += weight;
        }

        var origin = weightSum > 0.001f
            ? weightedPosition / weightSum
            : bestPoint.Position;
        var sternWidth = sternSampleCount > 1
            ? MathF.Max(MovementWakeMinWidth, sternMaxLateral - sternMinLateral)
            : MovementWakeMinWidth;
        var rawSpeedFactor = Math.Clamp((motion.Speed - MovementWaveMinSpeed) / 0.9f, 0f, 1f);
        var speedFactor = MathHelper.Lerp(0.25f, 1f, rawSpeedFactor);
        var lowSpeedFactor = 1f - rawSpeedFactor;
        var wakeWidth = sternWidth + MovementWakeWidthPadding;
        var outerWakeWidth = sternWidth * (1.14f + speedFactor * 0.16f) + MovementWakeWidthPadding * 1.5f;
        var mainSpan = 0.86f + speedFactor * 0.08f;
        var outerSpan = 1.02f + speedFactor * 0.1f;
        var mainRadius = GetWakeRadiusForWidth(wakeWidth, mainSpan);
        var outerRadius = GetWakeRadiusForWidth(outerWakeWidth, outerSpan);
        var wideShipFactor = Math.Clamp((sternWidth - 1f) / 2.4f, 0f, 1f);
        var wakeFrequencyScale = MathHelper.Lerp(1f, MovementWakeMinFrequencyScale, wideShipFactor);
        var movingWaveDelayScale = MathHelper.Lerp(1f, MovementWakeMaxDelayScale, wideShipFactor) *
            MathHelper.Lerp(1f, 1.3f, lowSpeedFactor);
        var wakeLifetimeScale = MathHelper.Lerp(1f, MovementWakeMaxLifetimeScale, wideShipFactor);
        var aftOffset = MovementWakeBaseAftOffset +
            sternWidth * MovementWakeWidthAftOffset +
            lowSpeedFactor * MovementWakeLowSpeedAftBoost;
        origin += waveDirection * aftOffset;

        EmitWaveParticle(
            mapId,
            origin,
            waveDirection,
            0.1f + speedFactor * 0.06f + sternWidth * 0.01f,
            0.18f + speedFactor * 0.1f + sternWidth * 0.045f,
            mainRadius,
            0.024f + speedFactor * 0.011f + sternWidth * 0.006f,
            0.004f + speedFactor * 0.002f + sternWidth * 0.0008f,
            mainSpan,
            0.2f + speedFactor * 0.08f + sternWidth * 0.02f,
            RippleAlpha * (0.86f + speedFactor * 0.22f),
            (0.96f + speedFactor * 0.22f + sternWidth * 0.14f) * wakeLifetimeScale,
            motion.WaveSeed + 5.1f + _random.NextFloat() * 0.7f,
            1.12f + sternWidth * 0.2f,
            wakeFrequencyScale);

        EmitWaveParticle(
            mapId,
            origin,
            waveDirection,
            0.076f + speedFactor * 0.04f + sternWidth * 0.008f,
            0.16f + speedFactor * 0.08f + sternWidth * 0.04f,
            outerRadius,
            0.019f + speedFactor * 0.008f + sternWidth * 0.0048f,
            0.0036f + speedFactor * 0.0018f + sternWidth * 0.0007f,
            outerSpan,
            0.18f + speedFactor * 0.07f + sternWidth * 0.018f,
            RippleAlpha * (0.55f + speedFactor * 0.16f),
            (1.16f + speedFactor * 0.24f + sternWidth * 0.18f) * (wakeLifetimeScale * 1.05f),
            motion.WaveSeed + 6.4f + _random.NextFloat() * 0.9f,
            1.26f + sternWidth * 0.24f,
            wakeFrequencyScale * 0.72f);

        motion.NextMovingWaveTime = (float) _timing.CurTime.TotalSeconds +
            _random.NextFloat(MovingWaveMinDelay, MovingWaveMaxDelay) * movingWaveDelayScale;
    }

    private void DrawWaveParticles(DrawingHandleWorld handle, MapId mapId)
    {
        foreach (var particle in _waveParticles)
        {
            if (particle.MapId != mapId)
                continue;

            var life = particle.Age / particle.Lifetime;
            var fadeIn = SmoothStep(0f, 0.18f, life);
            var fadeOut = 1f - SmoothStep(0.62f, 1f, life);
            var alpha = particle.Alpha * fadeIn * fadeOut;

            if (alpha <= 0.002f)
                continue;

            DrawArcBand(
                handle,
                particle.Center,
                particle.Direction,
                particle.Radius,
                particle.Thickness,
                particle.Span,
                particle.WaveAmplitudeScale,
                particle.WaveFrequencyScale,
                particle.Seed,
                Color.White.WithAlpha(alpha));
        }
    }

    private void DrawArcBand(
        DrawingHandleWorld handle,
        Vector2 origin,
        Vector2 forward,
        float radius,
        float thickness,
        float span,
        float waveAmplitudeScale,
        float waveFrequencyScale,
        float seed,
        Color color)
    {
        if (forward.LengthSquared() <= 0.0001f)
            return;

        forward = Vector2.Normalize(forward);
        var inner = new Vector2[ArcPointCount];
        var outer = new Vector2[ArcPointCount];
        var circleCenter = origin - forward * radius;
        var time = (float) _timing.CurTime.TotalSeconds * 1.1f;

        for (var i = 0; i < ArcPointCount; i++)
        {
            var t = i / (ArcPointCount - 1f);
            var signedAngle = MathHelper.Lerp(-span, span, t);
            var dir = Rotate(forward, signedAngle);
            var edgeFade = MathF.Sin(t * MathF.PI);
            var waveStrength = (0.012f + thickness * 0.26f + radius * 0.032f) * waveAmplitudeScale;
            var arcSizeFactor = MathF.Max(1f, radius / 1.05f);
            var waveFrequency = MathF.Max(0.2f, (1.85f * waveFrequencyScale) / arcSizeFactor);
            var wave = MathF.Sin(t * MathF.PI * waveFrequency + time + seed) * waveStrength * edgeFade;
            var localThickness = thickness * (0.18f + edgeFade * 0.82f);

            inner[i] = circleCenter + dir * (radius + wave);
            outer[i] = circleCenter + dir * (radius + wave + localThickness);
        }

        DrawBand(handle, inner, outer, color);
    }

    private void EmitWaveParticle(
        MapId mapId,
        Vector2 worldCenter,
        Vector2 worldDirection,
        float velocity,
        float radiusGrowth,
        float radius,
        float thickness,
        float thicknessGrowth,
        float span,
        float spanGrowth,
        float alpha,
        float lifetime,
        float seed,
        float waveAmplitudeScale,
        float waveFrequencyScale)
    {
        _waveParticles.Add(new WaveParticle
        {
            MapId = mapId,
            Center = worldCenter,
            Direction = Normalize(worldDirection),
            Velocity = Normalize(worldDirection) * velocity,
            Radius = radius,
            RadiusGrowth = radiusGrowth,
            Thickness = thickness,
            ThicknessGrowth = thicknessGrowth,
            Span = span,
            SpanGrowth = spanGrowth,
            Alpha = alpha,
            Lifetime = lifetime,
            Seed = seed,
            WaveAmplitudeScale = waveAmplitudeScale,
            WaveFrequencyScale = waveFrequencyScale,
        });
    }

    private ShipMotionState UpdateShipMotion(EntityUid uid, Vector2 worldPosition)
    {
        var now = (float) _timing.CurTime.TotalSeconds;
        if (!_shipStates.TryGetValue(uid, out var state))
        {
            state = new ShipMotionState
            {
                LastPosition = worldPosition,
                LastTime = now,
                WaveSeed = Hash(uid.Id * 17 + 3) * MathF.PI * 2f,
                NextSmallWaveTime = now + _random.NextFloat(SmallWaveMinDelay, SmallWaveMaxDelay),
                NextMovingWaveTime = now + _random.NextFloat(MovingWaveMinDelay, MovingWaveMaxDelay),
                InstantWorldDirection = Vector2.UnitY,
            };
            _shipStates[uid] = state;
            return state;
        }

        var deltaTime = MathF.Max(now - state.LastTime, 0.0001f);
        var delta = worldPosition - state.LastPosition;
        var velocity = delta / deltaTime;
        var speed = velocity.Length();

        if (speed > 0.0001f)
        {
            var direction = velocity / speed;
            state.InstantWorldDirection = direction;
            state.WorldDirection = Vector2.Lerp(state.WorldDirection, direction, MathF.Min(1f, deltaTime * 6f));
        }

        state.Speed += (speed - state.Speed) * MathF.Min(1f, deltaTime * 5f);
        state.LastPosition = worldPosition;
        state.LastTime = now;
        _shipStates[uid] = state;
        return state;
    }

    private sealed class ShipMotionState
    {
        public Vector2 LastPosition;
        public float LastTime;
        public float Speed;
        public float WaveSeed;
        public float NextSmallWaveTime;
        public float NextMovingWaveTime;
        public Vector2 InstantWorldDirection = Vector2.UnitY;
        public Vector2 WorldDirection = Vector2.UnitY;
    }

    private sealed class WaveParticle
    {
        public MapId MapId;
        public Vector2 Center;
        public Vector2 Direction;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Radius;
        public float RadiusGrowth;
        public float Thickness;
        public float ThicknessGrowth;
        public float Span;
        public float SpanGrowth;
        public float Alpha;
        public float Seed;
        public float WaveAmplitudeScale;
        public float WaveFrequencyScale;
    }

    private sealed class RippleEmitPoint
    {
        public Vector2 Position;
        public Vector2 Direction;
    }
}
