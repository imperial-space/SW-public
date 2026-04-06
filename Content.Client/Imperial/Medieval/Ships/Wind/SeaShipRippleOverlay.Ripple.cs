using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed partial class SeaShipRippleOverlay
{
    private void DrawRipple(
        DrawingHandleWorld handle,
        MapId mapId,
        EntityUid gridUid,
        MapGridComponent grid,
        Matrix3x2 worldMatrix,
        Box2 visibleBounds,
        ShipMask shipMask,
        EntityLookupSystem lookupSystem,
        ShipMotionState motion)
    {
        _occupiedTiles = shipMask.OccupiedTiles;
        _visibleOccupiedTiles.Clear();

        var tileEnumerator = MapSystem.GetTilesEnumerator(gridUid, grid, visibleBounds);
        while (tileEnumerator.MoveNext(out var tileRef))
        {
            _visibleOccupiedTiles.Add(tileRef.GridIndices);
        }

        if (_visibleOccupiedTiles.Count == 0 || _occupiedTiles.Count == 0)
            return;

        handle.SetTransform(worldMatrix);
        _visitedVertices.Clear();
        _emitPoints.Clear();
        _emitWorldMatrix = worldMatrix;
        var shipCenter = Vector2.Transform(grid.LocalAABB.Center, worldMatrix);

        foreach (var tile in _visibleOccupiedTiles)
        {
            if (HasBoundary(tile, Direction.North))
                DrawHorizontalRun(handle, lookupSystem, gridUid, grid, tile, 1, Direction.North, worldMatrix);

            if (HasBoundary(tile, Direction.South))
                DrawHorizontalRun(handle, lookupSystem, gridUid, grid, tile, 0, Direction.South, worldMatrix);

            if (HasBoundary(tile, Direction.East))
                DrawVerticalRun(handle, lookupSystem, gridUid, grid, tile, 1, Direction.East, worldMatrix);

            if (HasBoundary(tile, Direction.West))
                DrawVerticalRun(handle, lookupSystem, gridUid, grid, tile, 0, Direction.West, worldMatrix);
        }

        DrawCornerPatches(handle);

        handle.SetTransform(Matrix3x2.Identity);

        TryEmitSmallWave(mapId, gridUid, motion);
        TryEmitMovingWave(mapId, gridUid, motion, shipCenter);
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
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i startTile,
        int localYSide,
        Direction direction,
        Matrix3x2 worldMatrix)
    {
        var startBounds = lookupSystem.GetLocalBounds(startTile, grid.TileSize);
        var y = localYSide == 1 ? startBounds.Top : startBounds.Bottom;
        var startVertex = new Vector2i(startTile.X, localYSide == 1 ? startTile.Y + 1 : startTile.Y);
        var endVertex = new Vector2i(startTile.X + 1, localYSide == 1 ? startTile.Y + 1 : startTile.Y);

        var start = new Vector2(startBounds.Left, y);
        var end = new Vector2(startBounds.Right, y);

        if (GetCornerType(startVertex) == CornerType.Concave)
            start.X += ConcavePatchSize;

        if (GetCornerType(endVertex) == CornerType.Concave)
            end.X -= ConcavePatchSize;

        var outward = direction == Direction.North ? Vector2.UnitY : -Vector2.UnitY;
        var tileStep = startBounds.Right - startBounds.Left;
        DrawRippleBand(handle, start, end, outward, gridUid, startTile, new Vector2i(1, 0), direction == Direction.North ? 0 : 2, worldMatrix, tileStep);
    }

    private void DrawVerticalRun(
        DrawingHandleWorld handle,
        EntityLookupSystem lookupSystem,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i startTile,
        int localXSide,
        Direction direction,
        Matrix3x2 worldMatrix)
    {
        var startBounds = lookupSystem.GetLocalBounds(startTile, grid.TileSize);
        var x = localXSide == 1 ? startBounds.Right : startBounds.Left;
        var startVertex = new Vector2i(localXSide == 1 ? startTile.X + 1 : startTile.X, startTile.Y);
        var endVertex = new Vector2i(localXSide == 1 ? startTile.X + 1 : startTile.X, startTile.Y + 1);

        var start = new Vector2(x, startBounds.Bottom);
        var end = new Vector2(x, startBounds.Top);

        if (GetCornerType(startVertex) == CornerType.Concave)
            start.Y += ConcavePatchSize;

        if (GetCornerType(endVertex) == CornerType.Concave)
            end.Y -= ConcavePatchSize;

        var outward = direction == Direction.East ? Vector2.UnitX : -Vector2.UnitX;
        var tileStep = startBounds.Top - startBounds.Bottom;
        DrawRippleBand(handle, start, end, outward, gridUid, startTile, new Vector2i(0, 1), direction == Direction.East ? 1 : 3, worldMatrix, tileStep);
    }

    private void DrawRippleBand(
        DrawingHandleWorld handle,
        Vector2 start,
        Vector2 end,
        Vector2 outward,
        EntityUid gridUid,
        Vector2i runOriginTile,
        Vector2i runAxis,
        int edgeIndex,
        Matrix3x2 worldMatrix,
        float tileStep)
    {
        var outer = new Vector2[EdgePointCount];
        var direction = end - start;
        var edgeLength = direction.Length();

        if (edgeLength <= 0.001f)
            return;

        var waveCount = MathF.Max(1.35f, edgeLength / 2.8f);
        var tileCount = MathF.Max(1f, edgeLength / MathF.Max(tileStep, 0.001f));
        var time = (float) _timing.CurTime.TotalSeconds * RippleScrollSpeed;
        var shipSeed = gridUid.Id;
        var tilePhase =
            (runOriginTile.X * 0.73f) +
            (runOriginTile.Y * 1.17f) +
            (runAxis.X * 2.03f) +
            (runAxis.Y * 3.11f) +
            (edgeIndex * 0.91f) +
            (tileCount * 0.37f) +
            (shipSeed * 0.019f);

        for (var i = 0; i < outer.Length; i++)
        {
            var t = i / (outer.Length - 1f);
            var along = Vector2.Lerp(start, end, t);
            var edgeFade = MathF.Sin(t * MathF.PI);
            var wavePhase = t * MathF.PI * waveCount + time + edgeIndex * 0.75f + tilePhase;
            var waveOffset = MathF.Sin(wavePhase) * RippleAmplitude * edgeFade;
            var microPhase = t * MathF.PI * (waveCount * 3.35f + 1.9f) - time * 0.72f + edgeIndex * 1.31f + tilePhase * 1.43f;
            var microWave = MathF.Sin(microPhase) * RippleAmplitude * 0.34f * edgeFade;
            var crestPhase = t * MathF.PI * (waveCount * 5.2f + 2.4f) + time * 0.38f + edgeIndex * 0.53f + tilePhase * 0.91f;
            var crestWave = MathF.Sin(crestPhase) * RippleAmplitude * 0.11f * edgeFade;
            var outerOffset = MathF.Max(RippleOffset * 0.36f, RippleOffset * 0.95f + waveOffset + microWave + crestWave);

            outer[i] = along + outward * outerOffset;
        }

        var tangent = Normalize(end - start);
        var shader = GetRippleShader();
        var bandMatrix = new Matrix3x2(
            tangent.X * edgeLength,
            tangent.Y * edgeLength,
            outward.X * RippleBandExtent,
            outward.Y * RippleBandExtent,
            start.X,
            start.Y);

        shader.SetParameter("time", time);
        shader.SetParameter("edgeLength", edgeLength);
        shader.SetParameter("bandExtent", RippleBandExtent);
        shader.SetParameter("rippleOffset", RippleOffset);
        shader.SetParameter("rippleAmplitude", RippleAmplitude);
        shader.SetParameter("rippleAlpha", RippleAlpha);
        shader.SetParameter("waveCount", waveCount);
        shader.SetParameter("tilePhase", tilePhase);
        shader.SetParameter("tileStep", tileStep);
        shader.SetParameter("tileOriginX", (float) runOriginTile.X);
        shader.SetParameter("tileOriginY", (float) runOriginTile.Y);
        shader.SetParameter("tileAxisX", (float) runAxis.X);
        shader.SetParameter("tileAxisY", (float) runAxis.Y);
        shader.SetParameter("shipSeed", (float) shipSeed);
        shader.SetParameter("seedBase",
            runOriginTile.X * 3.87f +
            runOriginTile.Y * 5.21f +
            runAxis.X * 7.13f +
            runAxis.Y * 11.71f +
            edgeIndex * 13.37f +
            tileCount * 0.91f +
            shipSeed * 0.37f);
        shader.SetParameter("edgeIndex", (float) edgeIndex);
        shader.SetParameter("bubbleCount", 6);

        handle.UseShader(shader);
        handle.SetTransform(Matrix3x2.Multiply(bandMatrix, worldMatrix));
        handle.DrawRect(new Box2(Vector2.Zero, Vector2.One), Color.White);
        handle.UseShader(null);
        handle.SetTransform(worldMatrix);

        var waveletCount = edgeLength < 1.8f ? 1 : 2;
        var tangentDir = Normalize(end - start);

        for (var i = 0; i < waveletCount; i++)
        {
            var waveletSeed = runOriginTile.X * 2.17f + runOriginTile.Y * 1.13f + edgeIndex * 1.41f + i * 4.73f;
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
                tangentDir,
                outward,
                length,
                thickness,
                time,
                tilePhase + waveletSeed,
                Color.White.WithAlpha(RippleAlpha * 0.42f * fade));
        }

        if (outer.Length < 6)
            return;

        var worldDirection = Normalize(Vector2.TransformNormal(outward, _emitWorldMatrix));
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[2], _emitWorldMatrix), Direction = worldDirection });
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[outer.Length / 2], _emitWorldMatrix), Direction = worldDirection });
        _emitPoints.Add(new RippleEmitPoint { Position = Vector2.Transform(outer[outer.Length - 3], _emitWorldMatrix), Direction = worldDirection });
    }

    private ShaderInstance GetRippleShader()
    {
        if (_activeRippleShaderCount >= _rippleShaders.Count)
            _rippleShaders.Add(_baseRippleShader.Duplicate());

        return _rippleShaders[_activeRippleShaderCount++];
    }

    private void DrawCornerPatches(DrawingHandleWorld handle)
    {
        var color = Color.White.WithAlpha(RippleAlpha);

        foreach (var tile in _visibleOccupiedTiles)
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

        DrawLitPrimitives(handle, quadVerts, color);
    }

    private void DrawConvexCornerPatch(
        DrawingHandleWorld handle,
        Vector2 center,
        Vector2 dirA,
        Vector2 dirB,
        Color color)
    {
        var vertices = new Vector2[ConvexCornerSegments * 3];
        var vertexIndex = 0;
        var outerOffset = MathF.Max(RippleOffset * 0.36f, RippleOffset * 0.95f) + ConvexCornerOverlap;

        for (var i = 0; i < ConvexCornerSegments; i++)
        {
            var t0 = i / (float) ConvexCornerSegments;
            var t1 = (i + 1) / (float) ConvexCornerSegments;
            var angle0 = t0 * MathF.PI * 0.5f;
            var angle1 = t1 * MathF.PI * 0.5f;
            var p0 = center + (dirA * MathF.Cos(angle0) + dirB * MathF.Sin(angle0)) * outerOffset;
            var p1 = center + (dirA * MathF.Cos(angle1) + dirB * MathF.Sin(angle1)) * outerOffset;

            vertices[vertexIndex++] = center;
            vertices[vertexIndex++] = p0;
            vertices[vertexIndex++] = p1;
        }

        DrawLitPrimitives(handle, vertices, color);
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

    private void DrawBand(DrawingHandleWorld handle, Vector2[] inner, Vector2[] outer, Color color)
    {
        var vertices = new DrawVertexUV2D[(inner.Length - 1) * 6];
        var vertexIndex = 0;

        for (var i = 0; i < inner.Length - 1; i++)
        {
            vertices[vertexIndex++] = new DrawVertexUV2D(inner[i], inner[i]);
            vertices[vertexIndex++] = new DrawVertexUV2D(outer[i], outer[i]);
            vertices[vertexIndex++] = new DrawVertexUV2D(outer[i + 1], outer[i + 1]);
            vertices[vertexIndex++] = new DrawVertexUV2D(inner[i], inner[i]);
            vertices[vertexIndex++] = new DrawVertexUV2D(outer[i + 1], outer[i + 1]);
            vertices[vertexIndex++] = new DrawVertexUV2D(inner[i + 1], inner[i + 1]);
        }

        handle.UseShader(_detailRippleShader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _whiteTexture, vertices, color);
        handle.UseShader(null);
    }

    private void DrawLitPrimitives(DrawingHandleWorld handle, Vector2[] vertices, Color color)
    {
        var texturedVertices = new DrawVertexUV2D[vertices.Length];

        for (var i = 0; i < vertices.Length; i++)
        {
            texturedVertices[i] = new DrawVertexUV2D(vertices[i], vertices[i]);
        }

        handle.UseShader(_detailRippleShader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _whiteTexture, texturedVertices, color);
        handle.UseShader(null);
    }

    private void DrawDetachedWavelet(
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
}
