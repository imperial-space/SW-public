using System.Numerics;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed class SeaShipRippleOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int EdgePointCount = 24;
    private const float RippleOffset = 0.106f;
    private const float RippleAmplitude = 0.032f;
    private const float RippleScrollSpeed = 1.75f;
    private const float RippleAlpha = 0.58f;
    private const float TileQueryPadding = 3f;
    private const float ConcavePatchSize = 0.11f;
    private const int ConvexCornerSegments = 6;
    private const int ArcPointCount = 18;
    private const int DetachedWaveletPointCount = 8;
    private const int RippleCutoutAlongSubdiv = 10;
    private const int RippleCutoutAcrossSubdiv = 8;
    private const float MovementWaveMinSpeed = 0.14f;
    private const float SmallWaveMinDelay = 0.16f;
    private const float SmallWaveMaxDelay = 0.34f;
    private const float MovingWaveMinDelay = 0.28f;
    private const float MovingWaveMaxDelay = 0.46f;

    private readonly HashSet<Vector2i> _occupiedTiles = new();
    private readonly HashSet<Vector2i> _visitedVertices = new();
    private readonly Dictionary<EntityUid, ShipMotionState> _shipStates = new();
    private readonly HashSet<EntityUid> _activeShips = new();
    private readonly List<WaveParticle> _waveParticles = new();
    private readonly List<RippleEmitPoint> _emitPoints = new();
    private Matrix3x2 _emitWorldMatrix = Matrix3x2.Identity;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public SeaShipRippleOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 15;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        for (var i = _waveParticles.Count - 1; i >= 0; i--)
        {
            var particle = _waveParticles[i];
            particle.Age += args.DeltaSeconds;
            particle.Center += particle.Velocity * args.DeltaSeconds;
            particle.Radius += particle.RadiusGrowth * args.DeltaSeconds;
            particle.Thickness += particle.ThicknessGrowth * args.DeltaSeconds;
            particle.Span += particle.SpanGrowth * args.DeltaSeconds;

            if (particle.Age >= particle.Lifetime)
                _waveParticles.RemoveAt(i);
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) && !sea.Disabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var visibleBounds = args.WorldAABB.Enlarged(TileQueryPadding);
        var xformSystem = _entityManager.System<SharedTransformSystem>();
        var mapSystem = _entityManager.System<SharedMapSystem>();
        var lookupSystem = _entityManager.System<EntityLookupSystem>();
        var query = _entityManager.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
        _activeShips.Clear();

        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            if (!_entityManager.HasComponent<ShipDrowningComponent>(uid))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var worldMatrix = xformSystem.GetWorldMatrix(xform);
            var worldBounds = worldMatrix.TransformBox(grid.LocalAABB);

            if (!worldBounds.Intersects(visibleBounds))
                continue;

            _activeShips.Add(uid);
            var motion = UpdateShipMotion(uid, xformSystem.GetWorldPosition(uid));
            DrawRipple(args.WorldHandle, args.MapId, uid, grid, worldMatrix, visibleBounds, mapSystem, lookupSystem, motion);
        }

        var staleShips = new List<EntityUid>();
        foreach (var (uid, _) in _shipStates)
        {
            if (!_activeShips.Contains(uid))
                staleShips.Add(uid);
        }

        foreach (var uid in staleShips)
        {
            _shipStates.Remove(uid);
        }

        DrawWaveParticles(args.WorldHandle, args.MapId);
    }

    private void DrawRipple(
        DrawingHandleWorld handle,
        MapId mapId,
        EntityUid gridUid,
        MapGridComponent grid,
        Matrix3x2 worldMatrix,
        Box2 visibleBounds,
        SharedMapSystem mapSystem,
        EntityLookupSystem lookupSystem,
        ShipMotionState motion)
    {
        _occupiedTiles.Clear();
        var tileEnumerator = mapSystem.GetTilesEnumerator(gridUid, grid, visibleBounds);

        while (tileEnumerator.MoveNext(out var tileRef))
        {
            _occupiedTiles.Add(tileRef.GridIndices);
        }

        if (_occupiedTiles.Count == 0)
            return;

        handle.SetTransform(worldMatrix);
        _visitedVertices.Clear();
        _emitPoints.Clear();
        _emitWorldMatrix = worldMatrix;
        var shipCenter = Vector2.Transform(grid.LocalAABB.Center, worldMatrix);

        foreach (var tile in _occupiedTiles)
        {
            if (HasBoundary(tile, Direction.North) && !HasBoundary(tile + new Vector2i(-1, 0), Direction.North))
                DrawHorizontalRun(handle, lookupSystem, grid, tile, 1, Direction.North);

            if (HasBoundary(tile, Direction.South) && !HasBoundary(tile + new Vector2i(-1, 0), Direction.South))
                DrawHorizontalRun(handle, lookupSystem, grid, tile, 0, Direction.South);

            if (HasBoundary(tile, Direction.East) && !HasBoundary(tile + new Vector2i(0, -1), Direction.East))
                DrawVerticalRun(handle, lookupSystem, grid, tile, 1, Direction.East);

            if (HasBoundary(tile, Direction.West) && !HasBoundary(tile + new Vector2i(0, -1), Direction.West))
                DrawVerticalRun(handle, lookupSystem, grid, tile, 0, Direction.West);
        }

        DrawCornerPatches(handle);

        handle.SetTransform(Matrix3x2.Identity);

        TryEmitSmallWave(mapId, motion);
        TryEmitMovingWave(mapId, motion, shipCenter);
    }

    private bool HasBoundary(Vector2i tile, Direction direction)
    {
        if (!_occupiedTiles.Contains(tile))
            return false;

        var neighbor = direction switch
        {
            Direction.North => tile + new Vector2i(0, 1),
            Direction.South => tile + new Vector2i(0, -1),
            Direction.East => tile + new Vector2i(1, 0),
            Direction.West => tile + new Vector2i(-1, 0),
            _ => tile,
        };

        return !_occupiedTiles.Contains(neighbor);
    }

    private void DrawHorizontalRun(
        DrawingHandleWorld handle,
        EntityLookupSystem lookupSystem,
        MapGridComponent grid,
        Vector2i startTile,
        int localYSide,
        Direction direction)
    {
        var endTile = startTile;
        while (HasBoundary(endTile + new Vector2i(1, 0), direction))
        {
            endTile += new Vector2i(1, 0);
        }

        var startBounds = lookupSystem.GetLocalBounds(startTile, grid.TileSize);
        var endBounds = lookupSystem.GetLocalBounds(endTile, grid.TileSize);
        var y = localYSide == 1 ? startBounds.Top : startBounds.Bottom;
        var startVertex = new Vector2i(startTile.X, localYSide == 1 ? startTile.Y + 1 : startTile.Y);
        var endVertex = new Vector2i(endTile.X + 1, localYSide == 1 ? endTile.Y + 1 : endTile.Y);

        var start = new Vector2(startBounds.Left, y);
        var end = new Vector2(endBounds.Right, y);

        if (GetCornerType(startVertex) == CornerType.Concave)
            start.X += ConcavePatchSize;

        if (GetCornerType(endVertex) == CornerType.Concave)
            end.X -= ConcavePatchSize;

        var outward = direction == Direction.North ? Vector2.UnitY : -Vector2.UnitY;
        var seedTile = startTile + endTile;
        DrawRippleBand(handle, start, end, outward, seedTile, direction == Direction.North ? 0 : 2);
    }

    private void DrawVerticalRun(
        DrawingHandleWorld handle,
        EntityLookupSystem lookupSystem,
        MapGridComponent grid,
        Vector2i startTile,
        int localXSide,
        Direction direction)
    {
        var endTile = startTile;
        while (HasBoundary(endTile + new Vector2i(0, 1), direction))
        {
            endTile += new Vector2i(0, 1);
        }

        var startBounds = lookupSystem.GetLocalBounds(startTile, grid.TileSize);
        var endBounds = lookupSystem.GetLocalBounds(endTile, grid.TileSize);
        var x = localXSide == 1 ? startBounds.Right : startBounds.Left;
        var startVertex = new Vector2i(localXSide == 1 ? startTile.X + 1 : startTile.X, startTile.Y);
        var endVertex = new Vector2i(localXSide == 1 ? endTile.X + 1 : endTile.X, endTile.Y + 1);

        var start = new Vector2(x, startBounds.Bottom);
        var end = new Vector2(x, endBounds.Top);

        if (GetCornerType(startVertex) == CornerType.Concave)
            start.Y += ConcavePatchSize;

        if (GetCornerType(endVertex) == CornerType.Concave)
            end.Y -= ConcavePatchSize;

        var outward = direction == Direction.East ? Vector2.UnitX : -Vector2.UnitX;
        var seedTile = startTile + endTile;
        DrawRippleBand(handle, start, end, outward, seedTile, direction == Direction.East ? 1 : 3);
    }

    private void DrawRippleBand(
        DrawingHandleWorld handle,
        Vector2 start,
        Vector2 end,
        Vector2 outward,
        Vector2i tileSeed,
        int edgeIndex)
    {
        var inner = new Vector2[EdgePointCount];
        var outer = new Vector2[EdgePointCount];
        var direction = end - start;
        var edgeLength = direction.Length();

        if (edgeLength <= 0.001f)
            return;

        var waveCount = MathF.Max(1.35f, edgeLength / 2.8f);
        var time = (float) _timing.CurTime.TotalSeconds * RippleScrollSpeed;
        var tilePhase = (tileSeed.X * 0.73f) + (tileSeed.Y * 1.17f);

        for (var i = 0; i < inner.Length; i++)
        {
            var t = i / (inner.Length - 1f);
            var along = Vector2.Lerp(start, end, t);
            var edgeFade = MathF.Sin(t * MathF.PI);
            var wavePhase = t * MathF.PI * waveCount + time + edgeIndex * 0.75f + tilePhase;
            var waveOffset = MathF.Sin(wavePhase) * RippleAmplitude * edgeFade;
            var microPhase = t * MathF.PI * (waveCount * 3.35f + 1.9f) - time * 0.72f + edgeIndex * 1.31f + tilePhase * 1.43f;
            var microWave = MathF.Sin(microPhase) * RippleAmplitude * 0.34f * edgeFade;
            var crestPhase = t * MathF.PI * (waveCount * 5.2f + 2.4f) + time * 0.38f + edgeIndex * 0.53f + tilePhase * 0.91f;
            var crestWave = MathF.Sin(crestPhase) * RippleAmplitude * 0.11f * edgeFade;
            var innerOffset = MathF.Max(0f, waveOffset * 0.14f + microWave * 0.32f + crestWave * 0.18f);
            var outerOffset = MathF.Max(RippleOffset * 0.36f, RippleOffset * 0.95f + waveOffset + microWave + crestWave);

            inner[i] = along + outward * innerOffset;
            outer[i] = along + outward * outerOffset;
        }

        var cutouts = BuildRippleCutouts(edgeLength, tileSeed, edgeIndex, tilePhase, time);
        DrawBandWithCutouts(handle, inner, outer, start, Normalize(direction), outward, cutouts, Color.White.WithAlpha(RippleAlpha));
        DrawRippleDetails(handle, start, end, outward, tileSeed, edgeIndex, edgeLength, tilePhase, time);
        AddEmitPoints(outer, outward);
    }

    private RippleCutout[] BuildRippleCutouts(
        float edgeLength,
        Vector2i tileSeed,
        int edgeIndex,
        float tilePhase,
        float time)
    {
        var bubbleCount = edgeLength < 1.4f ? 4 : edgeLength < 3.0f ? 7 : 11;
        var cutouts = new RippleCutout[bubbleCount];

        for (var i = 0; i < bubbleCount; i++)
        {
            var bubbleSeed = tileSeed.X * 1.71f + tileSeed.Y * 2.43f + edgeIndex * 0.91f + i * 3.17f;
            var bubbleHash = Hash((int) (bubbleSeed * 1000f));
            var travel = Frac(bubbleHash + time * (0.28f + bubbleHash * 0.085f));
            var fade = SmoothStep(0.015f, 0.08f, travel) * (1f - SmoothStep(0.9f, 0.985f, travel));
            var progress = SmoothStep(0.015f, 0.82f, travel);
            var t = 0.12f + bubbleHash * 0.74f +
                    MathF.Sin(time * (0.82f + bubbleHash * 0.34f) + tilePhase + i) * 0.03f;
            t = Math.Clamp(t, 0.08f, 0.92f);
            var innerOffset = 0.008f + bubbleHash * 0.008f;
            var outerOffset = RippleOffset * (0.72f + bubbleHash * 0.2f);
            var radialOffset = MathHelper.Lerp(innerOffset, outerOffset, progress);
            var radiusAlong = MathHelper.Lerp(0.013f + bubbleHash * 0.0096f, 0.034f + bubbleHash * 0.026f, progress) * fade;
            var radiusOutward = MathHelper.Lerp(0.0054f + bubbleHash * 0.0042f, 0.0148f + bubbleHash * 0.0108f, progress) * fade;

            cutouts[i] = new RippleCutout
            {
                Along = edgeLength * t,
                Outward = radialOffset,
                RadiusAlong = radiusAlong,
                RadiusOutward = radiusOutward,
                Rotation = (bubbleHash - 0.5f) * 0.45f,
                Irregularity = 0.075f + bubbleHash * 0.13f,
                Phase = tilePhase + bubbleSeed * 0.37f,
            };
        }

        return cutouts;
    }

    private void DrawRippleDetails(
        DrawingHandleWorld handle,
        Vector2 start,
        Vector2 end,
        Vector2 outward,
        Vector2i tileSeed,
        int edgeIndex,
        float edgeLength,
        float tilePhase,
        float time)
    {
        var tangent = Normalize(end - start);
        var waveletCount = edgeLength < 1.8f ? 1 : 2;

        for (var i = 0; i < waveletCount; i++)
        {
            var waveletSeed = tileSeed.X * 2.17f + tileSeed.Y * 1.13f + edgeIndex * 1.41f + i * 4.73f;
            var waveletHash = Hash((int) (waveletSeed * 1000f));
            var travel = Frac(waveletHash - time * (0.08f + waveletHash * 0.024f));
            var fade = SmoothStep(0.07f, 0.18f, travel) * (1f - SmoothStep(0.82f, 0.95f, travel));
            var t = 0.1f + travel * 0.8f;
            var center = Vector2.Lerp(start, end, t);
            var offset = RippleOffset + 0.024f + waveletHash * 0.015f;
            var length = 0.12f + waveletHash * 0.09f;
            var thickness = 0.008f + waveletHash * 0.004f;

            DrawDetachedWavelet(
                handle,
                center + outward * offset,
                tangent,
                outward,
                length,
                thickness,
                time,
                tilePhase + waveletSeed,
                Color.White.WithAlpha(RippleAlpha * 0.42f * fade));
        }
    }

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
            motion.WaveSeed + _random.NextFloat() * 3f);

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
        var bestPointLateral = float.MaxValue;
        RippleEmitPoint? bestPoint = null;

        foreach (var sample in _emitPoints)
        {
            var offset = sample.Position - shipCenter;
            var backProjection = Vector2.Dot(offset, waveDirection);
            var lateralProjection = MathF.Abs(Vector2.Dot(offset, sideDirection));

            if (!(backProjection > bestBackProjection + 0.001f) &&
                (!(MathF.Abs(backProjection - bestBackProjection) <= 0.001f) ||
                 !(lateralProjection < bestPointLateral)))
                continue;
            
            bestBackProjection = backProjection;
            bestPointLateral = lateralProjection;
            bestPoint = sample;
        }

        if (bestPoint == null)
            return;

        var weightedPosition = Vector2.Zero;
        var weightSum = 0f;
        var frontWindow = 0.16f;
        var lateralWindow = MathF.Max(0.12f, bestPointLateral + 0.08f);

        foreach (var sample in _emitPoints)
        {
            var offset = sample.Position - shipCenter;
            var backProjection = Vector2.Dot(offset, waveDirection);
            var lateralProjection = MathF.Abs(Vector2.Dot(offset, sideDirection));
            var facing = Vector2.Dot(sample.Direction, waveDirection);

            if (facing <= 0.1f)
                continue;

            if (bestBackProjection - backProjection > frontWindow)
                continue;

            var frontWeight = 1f - Math.Clamp((bestBackProjection - backProjection) / frontWindow, 0f, 1f);
            var sideWeight = 1f - Math.Clamp(lateralProjection / lateralWindow, 0f, 1f);
            var weight = facing * frontWeight * (0.2f + sideWeight * 0.8f);

            if (weight <= 0.001f)
                continue;

            weightedPosition += sample.Position * weight;
            weightSum += weight;
        }

        var origin = weightSum > 0.001f
            ? weightedPosition / weightSum
            : bestPoint.Position;

        var speedFactor = Math.Clamp((motion.Speed - MovementWaveMinSpeed) / 0.9f, 0.25f, 1f);

        EmitWaveParticle(
            mapId,
            origin,
            waveDirection,
            0.085f + speedFactor * 0.055f,
            0.15f + speedFactor * 0.09f,
            0.2f + speedFactor * 0.08f,
            0.018f + speedFactor * 0.008f,
            0.004f + speedFactor * 0.0025f,
            0.44f + speedFactor * 0.1f,
            0.2f + speedFactor * 0.08f,
            RippleAlpha * (0.82f + speedFactor * 0.22f),
            0.76f + speedFactor * 0.2f,
            motion.WaveSeed + 5.1f + _random.NextFloat() * 0.7f);

        motion.NextMovingWaveTime = (float) _timing.CurTime.TotalSeconds + _random.NextFloat(MovingWaveMinDelay, MovingWaveMaxDelay);
    }

    private void DrawCornerPatches(DrawingHandleWorld handle)
    {
        var color = Color.White.WithAlpha(RippleAlpha);

        foreach (var tile in _occupiedTiles)
        {
            DrawCornerPatch(handle, new Vector2i(tile.X + 1, tile.Y + 1), color);
            DrawCornerPatch(handle, new Vector2i(tile.X, tile.Y + 1), color);
            DrawCornerPatch(handle, new Vector2i(tile.X + 1, tile.Y), color);
            DrawCornerPatch(handle, new Vector2i(tile.X, tile.Y), color);
        }
    }

    private void DrawCornerPatch(DrawingHandleWorld handle, Vector2i vertex, Color color)
    {
        if (!_visitedVertices.Add(vertex))
            return;

        var cornerType = GetCornerType(vertex);

        if (cornerType == CornerType.Convex)
        {
            var dirs = GetConvexCornerDirections(vertex);
            if (dirs == null)
                return;

            DrawConvexCornerPatch(handle, new Vector2(vertex.X, vertex.Y), dirs.Value.Item1, dirs.Value.Item2, color);
            return;
        }

        if (cornerType != CornerType.Concave)
            return;

        var concaveDirs = GetConcaveCornerDirections(vertex);
        if (concaveDirs == null)
            return;

        var cornerPos2 = new Vector2(vertex.X, vertex.Y);
        var quadVerts = new[]
        {
            cornerPos2,
            cornerPos2 + concaveDirs.Value.Item1 * ConcavePatchSize,
            cornerPos2 + concaveDirs.Value.Item1 * ConcavePatchSize + concaveDirs.Value.Item2 * ConcavePatchSize,
            cornerPos2,
            cornerPos2 + concaveDirs.Value.Item1 * ConcavePatchSize + concaveDirs.Value.Item2 * ConcavePatchSize,
            cornerPos2 + concaveDirs.Value.Item2 * ConcavePatchSize,
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, quadVerts, color);
    }

    private static void DrawConvexCornerPatch(
        DrawingHandleWorld handle,
        Vector2 center,
        Vector2 dirA,
        Vector2 dirB,
        Color color)
    {
        var vertices = new Vector2[ConvexCornerSegments * 3];
        var vertexIndex = 0;

        for (var i = 0; i < ConvexCornerSegments; i++)
        {
            var t0 = i / (float) ConvexCornerSegments;
            var t1 = (i + 1) / (float) ConvexCornerSegments;

            var p0 = QuarterCirclePoint(center, dirA, dirB, t0);
            var p1 = QuarterCirclePoint(center, dirA, dirB, t1);

            vertices[vertexIndex++] = center;
            vertices[vertexIndex++] = p0;
            vertices[vertexIndex++] = p1;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private static Vector2 QuarterCirclePoint(Vector2 center, Vector2 dirA, Vector2 dirB, float t)
    {
        var angle = t * MathF.PI * 0.5f;
        var point = dirA * MathF.Cos(angle) + dirB * MathF.Sin(angle);
        return center + point * RippleOffset;
    }

    private CornerType GetCornerType(Vector2i vertex)
    {
        var occupiedCount = GetCornerOccupancyCount(vertex);

        return occupiedCount switch
        {
            1 => CornerType.Convex,
            3 => CornerType.Concave,
            _ => CornerType.None,
        };
    }

    private (Vector2, Vector2)? GetConvexCornerDirections(Vector2i vertex)
    {
        var sw = _occupiedTiles.Contains(vertex + new Vector2i(-1, -1));
        var se = _occupiedTiles.Contains(vertex + new Vector2i(0, -1));
        var nw = _occupiedTiles.Contains(vertex + new Vector2i(-1, 0));
        var ne = _occupiedTiles.Contains(vertex);

        if (sw && !se && !nw && !ne)
            return (Vector2.UnitX, Vector2.UnitY);

        if (se && !sw && !nw && !ne)
            return (-Vector2.UnitX, Vector2.UnitY);

        if (nw && !sw && !se && !ne)
            return (Vector2.UnitX, -Vector2.UnitY);

        if (ne && !sw && !se && !nw)
            return (-Vector2.UnitX, -Vector2.UnitY);

        return null;
    }

    private (Vector2, Vector2)? GetConcaveCornerDirections(Vector2i vertex)
    {
        var sw = _occupiedTiles.Contains(vertex + new Vector2i(-1, -1));
        var se = _occupiedTiles.Contains(vertex + new Vector2i(0, -1));
        var nw = _occupiedTiles.Contains(vertex + new Vector2i(-1, 0));
        var ne = _occupiedTiles.Contains(vertex);

        if (!ne && sw && se && nw)
            return (Vector2.UnitX, Vector2.UnitY);

        if (!sw && se && nw && ne)
            return (-Vector2.UnitX, -Vector2.UnitY);

        if (!nw && sw && se && ne)
            return (-Vector2.UnitX, Vector2.UnitY);

        if (!se && sw && nw && ne)
            return (Vector2.UnitX, -Vector2.UnitY);

        return null;
    }

    private static void DrawBand(DrawingHandleWorld handle, Vector2[] inner, Vector2[] outer, Color color)
    {
        var vertices = new Vector2[(inner.Length - 1) * 6];
        var vertexIndex = 0;

        for (var i = 0; i < inner.Length - 1; i++)
        {
            vertices[vertexIndex++] = inner[i];
            vertices[vertexIndex++] = outer[i];
            vertices[vertexIndex++] = outer[i + 1];
            vertices[vertexIndex++] = inner[i];
            vertices[vertexIndex++] = outer[i + 1];
            vertices[vertexIndex++] = inner[i + 1];
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private static void DrawBandWithCutouts(
        DrawingHandleWorld handle,
        Vector2[] inner,
        Vector2[] outer,
        Vector2 start,
        Vector2 tangent,
        Vector2 outward,
        RippleCutout[] cutouts,
        Color color)
    {
        if (color.A == 0)
            return;

        var vertices = new Vector2[(inner.Length - 1) * RippleCutoutAlongSubdiv * RippleCutoutAcrossSubdiv * 6];
        var vertexIndex = 0;

        for (var i = 0; i < inner.Length - 1; i++)
        {
            for (var alongStep = 0; alongStep < RippleCutoutAlongSubdiv; alongStep++)
            {
                var alongT0 = alongStep / (float) RippleCutoutAlongSubdiv;
                var alongT1 = (alongStep + 1) / (float) RippleCutoutAlongSubdiv;
                var inner0 = Vector2.Lerp(inner[i], inner[i + 1], alongT0);
                var outer0 = Vector2.Lerp(outer[i], outer[i + 1], alongT0);
                var inner1 = Vector2.Lerp(inner[i], inner[i + 1], alongT1);
                var outer1 = Vector2.Lerp(outer[i], outer[i + 1], alongT1);

                for (var crossStep = 0; crossStep < RippleCutoutAcrossSubdiv; crossStep++)
                {
                    var crossT0 = crossStep / (float) RippleCutoutAcrossSubdiv;
                    var crossT1 = (crossStep + 1) / (float) RippleCutoutAcrossSubdiv;
                    var p00 = Vector2.Lerp(inner0, outer0, crossT0);
                    var p01 = Vector2.Lerp(inner0, outer0, crossT1);
                    var p10 = Vector2.Lerp(inner1, outer1, crossT0);
                    var p11 = Vector2.Lerp(inner1, outer1, crossT1);
                    var center = (p00 + p01 + p10 + p11) * 0.25f;
                    var local = center - start;
                    var along = Vector2.Dot(local, tangent);
                    var radial = Vector2.Dot(local, outward);

                    if (IsInsideCutout(along, radial, cutouts))
                        continue;

                    vertices[vertexIndex++] = p00;
                    vertices[vertexIndex++] = p01;
                    vertices[vertexIndex++] = p11;
                    vertices[vertexIndex++] = p00;
                    vertices[vertexIndex++] = p11;
                    vertices[vertexIndex++] = p10;
                }
            }
        }

        if (vertexIndex == 0)
            return;

        if (vertexIndex == vertices.Length)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
            return;
        }

        var trimmed = new Vector2[vertexIndex];
        Array.Copy(vertices, trimmed, vertexIndex);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, trimmed, color);
    }

    private static bool IsInsideCutout(float along, float radial, RippleCutout[] cutouts)
    {
        foreach (var cutout in cutouts)
        {
            if (cutout.RadiusAlong <= 0.0005f || cutout.RadiusOutward <= 0.0005f)
                continue;

            var dx = along - cutout.Along;
            var dy = radial - cutout.Outward;
            var sin = MathF.Sin(cutout.Rotation);
            var cos = MathF.Cos(cutout.Rotation);
            var localX = dx * cos - dy * sin;
            var localY = dx * sin + dy * cos;
            var nx = localX / cutout.RadiusAlong;
            var ny = localY / cutout.RadiusOutward;
            var angle = MathF.Atan2(ny, nx);
            var wobble = 1f + MathF.Sin(angle * (3.1f + cutout.Irregularity * 4.2f) + cutout.Phase) * cutout.Irregularity;

            if (nx * nx + ny * ny <= wobble * wobble)
                return true;
        }

        return false;
    }

    private static void DrawDetachedWavelet(
        DrawingHandleWorld handle,
        Vector2 center,
        Vector2 tangent,
        Vector2 outward,
        float length,
        float thickness,
        float time,
        float seed,
        Color color)
    {
        if (color.A == 0)
            return;

        var inner = new Vector2[DetachedWaveletPointCount];
        var outer = new Vector2[DetachedWaveletPointCount];

        for (var i = 0; i < DetachedWaveletPointCount; i++)
        {
            var t = i / (DetachedWaveletPointCount - 1f);
            var signed = MathHelper.Lerp(-0.5f, 0.5f, t);
            var edgeFade = MathF.Sin(t * MathF.PI);
            var along = center + tangent * (signed * length);
            var flutter = MathF.Sin(t * MathF.PI * 2.3f + time * 1.4f + seed) * 0.007f * edgeFade;
            var bow = MathF.Sin(t * MathF.PI) * 0.012f;
            var baseOffset = bow + flutter;
            var localThickness = thickness * (0.24f + edgeFade * 0.76f);

            inner[i] = along + outward * baseOffset;
            outer[i] = along + outward * (baseOffset + localThickness);
        }

        DrawBand(handle, inner, outer, color);
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

            DrawArcBand(handle, particle.Center, particle.Direction, particle.Radius, particle.Thickness, particle.Span, particle.Seed, Color.White.WithAlpha(alpha));
        }
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
        float seed)
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
        });
    }

    private void AddEmitPoints(Vector2[] outer, Vector2 outward)
    {
        if (outer.Length < 6)
            return;

        var worldDirection = Normalize(Vector2.TransformNormal(outward, _emitWorldMatrix));
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[2], _emitWorldMatrix), Direction = worldDirection });
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[outer.Length / 2], _emitWorldMatrix), Direction = worldDirection });
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[outer.Length - 3], _emitWorldMatrix), Direction = worldDirection });
    }

    private void DrawArcBand(
        DrawingHandleWorld handle,
        Vector2 origin,
        Vector2 forward,
        float radius,
        float thickness,
        float span,
        float seed,
        Color color)
    {
        if (forward.LengthSquared() <= 0.0001f)
            return;

        forward = Vector2.Normalize(forward);
        var inner = new Vector2[ArcPointCount];
        var outer = new Vector2[ArcPointCount];
        var circleCenter = origin - forward * radius;
        var baseDirection = forward;
        var time = (float) _timing.CurTime.TotalSeconds * 1.1f;

        for (var i = 0; i < ArcPointCount; i++)
        {
            var t = i / (ArcPointCount - 1f);
            var signedAngle = MathHelper.Lerp(-span, span, t);
            var dir = Rotate(baseDirection, signedAngle);
            var edgeFade = MathF.Sin(t * MathF.PI);
            var wave = MathF.Sin(t * MathF.PI * 2.1f + time + seed) * 0.018f * edgeFade;
            var localThickness = thickness * (0.18f + edgeFade * 0.82f);

            inner[i] = circleCenter + dir * (radius + wave);
            outer[i] = circleCenter + dir * (radius + wave + localThickness);
        }

        DrawBand(handle, inner, outer, color);
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

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Vector2 Normalize(Vector2 vector)
    {
        return vector.LengthSquared() <= 0.0001f ? Vector2.UnitY : Vector2.Normalize(vector);
    }

    private static float Hash(int value)
    {
        var hashed = MathF.Sin(value * 12.9898f) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private static float Frac(float value)
    {
        return value - MathF.Floor(value);
    }

    private int GetCornerOccupancyCount(Vector2i vertex)
    {
        var occupiedCount = 0;

        if (_occupiedTiles.Contains(vertex + new Vector2i(-1, -1)))
            occupiedCount++;
        if (_occupiedTiles.Contains(vertex + new Vector2i(0, -1)))
            occupiedCount++;
        if (_occupiedTiles.Contains(vertex + new Vector2i(-1, 0)))
            occupiedCount++;
        if (_occupiedTiles.Contains(vertex))
            occupiedCount++;

        return occupiedCount;
    }

    private enum CornerType : byte
    {
        None,
        Convex,
        Concave,
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
    }

    private sealed class RippleEmitPoint
    {
        public Vector2 Position;
        public Vector2 Direction;
    }

    private sealed class RippleCutout
    {
        public float Along;
        public float Outward;
        public float RadiusAlong;
        public float RadiusOutward;
        public float Rotation;
        public float Irregularity;
        public float Phase;
    }
}
