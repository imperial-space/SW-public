using System.Linq;
using System.Numerics;
using Robust.Shared.Maths;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossBeamGeometryTest
{
    [TestCase(12.25f, 0f, 1f)]
    [TestCase(0f, 8f, 2f)]
    [TestCase(-6f, -9f, 3f)]
    [TestCase(3f, -4f, 0.5f)]
    public void SingleBeamFollowsArbitraryEndpointsAndWidth(float x, float y, float width)
    {
        var start = new Vector2(7, -3);
        var beam = new BossBeamPlacement(start, start + new Vector2(x, y), width);
        var animation = new BossBeamAnimationData();
        var geometry = BossBeamGeometry.Build(beam, new(256, 64), animation, 2);
        Assert.That(geometry, Has.Length.EqualTo(6));
        var direction = Vector2.Normalize(beam.End - beam.Start);
        var normal = new Vector2(-direction.Y, direction.X);
        foreach (var vertex in geometry)
        {
            var offset = vertex.Position - start;
            var pixel = vertex.TextureCoordinate * new Vector2(256, 64);
            Assert.That(Vector2.Dot(offset, direction),
                Is.EqualTo((pixel.X - 30) / 196 * new Vector2(x, y).Length()).Within(0.00001));
            Assert.That(Vector2.Dot(offset, normal),
                Is.EqualTo((32 - pixel.Y) / 16 * width / 2).Within(0.00001));
        }
    }

    [Test]
    public void InvalidBeamDataDoesNotEmitGeometry()
    {
        var animation = new BossBeamAnimationData();
        Assert.That(BossBeamGeometry.Build(new(Vector2.Zero, Vector2.Zero, 2), new(256, 64), animation, 2), Is.Empty);
        Assert.That(BossBeamGeometry.Build(new(Vector2.Zero, Vector2.One, -1), new(256, 64), animation, 2), Is.Empty);
        Assert.That(BossBeamGeometry.Build(new(Vector2.Zero, new(float.NaN, 1), 2), new(256, 64), animation, 2), Is.Empty);
        animation.PixelsPerTile = 0;
        Assert.That(BossBeamGeometry.Build(new(Vector2.Zero, Vector2.One, 2), new(256, 64), animation, 2), Is.Empty);
    }

    [Test]
    public void DiagonalClippingKeepsTextureCoordinatesAndDoesNotFillTheSafeArea()
    {
        var beam = new BossBeamPlacement(new(-2, -2), new(3, 3), 2);
        var area = new Box2(0, 0, 1, 1);
        var geometry = BossBeamGeometry.Build(beam, new(256, 64), new(), 2, [area]);
        Assert.That(geometry, Is.Not.Empty);
        var direction = Vector2.Normalize(beam.End - beam.Start);
        foreach (var vertex in geometry)
        {
            Assert.That(vertex.Position.X, Is.InRange(-0.00001, 1.00001));
            Assert.That(vertex.Position.Y, Is.InRange(-0.00001, 1.00001));
            Assert.That(vertex.TextureCoordinate.X,
                Is.EqualTo((30 + Vector2.Dot(vertex.Position - beam.Start, direction) / (beam.End - beam.Start).Length() * 196) / 256).Within(0.00001));
        }
        Assert.That(Covers(geometry, new(0.5f)), Is.True);
        Assert.That(Covers(geometry, new(1.5f)), Is.False);
    }

    [TestCase(2, false)] [TestCase(2, true)]
    [TestCase(5, false)] [TestCase(5, true)]
    [TestCase(7, false)] [TestCase(7, true)]
    public void RepeatedBeamsCoverDangerAndPreserveSafeCellsAtDifferentArenaSizes(int halfSize, bool secondPhase)
    {
        var pattern = BossTelegraphPattern.BlueFirst;
        var danger = BossTilePatterns.Danger(pattern, [], secondPhase, halfSize: halfSize);
        var allowed = BossConstellationVisuals.AllowedAreas(danger, halfSize);
        for (var first = 0; first < allowed.Length; first++)
        for (var second = first + 1; second < allowed.Length; second++)
        {
            var overlapX = Math.Min(allowed[first].Right, allowed[second].Right) - Math.Max(allowed[first].Left, allowed[second].Left);
            var overlapY = Math.Min(allowed[first].Top, allowed[second].Top) - Math.Max(allowed[first].Bottom, allowed[second].Bottom);
            Assert.That(overlapX <= 0 || overlapY <= 0, Is.True, "Clip rectangles must not multiply the same beam.");
        }
        var triangles = BossConstellationVisuals.Beams(pattern, secondPhase, halfSize)
            .SelectMany(beam => BossBeamGeometry.Build(beam, new(256, 64), new(), beam.Width, allowed)).ToArray();
        foreach (var tile in BossTilePatterns.GetPlatform(halfSize))
            Assert.That(Covers(triangles, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)), Is.EqualTo(danger.Contains(tile)), $"Cell {tile}");
    }

    [Test]
    public void AtlasCoordinatesRespectFrameOffsetsAndVerticalTextureDirection()
    {
        Assert.That(BossBeamRenderer.AtlasCoordinate(Vector2.Zero, new(256, 64), new(256, 128), new(1024, 512)),
            Is.EqualTo(new Vector2(0.25f, 0.75f)));
        Assert.That(BossBeamRenderer.AtlasCoordinate(Vector2.One, new(256, 64), new(256, 128), new(1024, 512)),
            Is.EqualTo(new Vector2(0.5f, 0.625f)));
    }

    [Test]
    public void TransparentCanvasPaddingPreservesTheOriginalVisibleBounds()
    {
        var content = new Box2(-1, -2, 1, 2);
        var canvas = BossSpriteCanvas.ExpandBounds(content, new(38, 38), new(64, 64));
        Assert.That(canvas.Left + canvas.Width * 13 / 64, Is.EqualTo(content.Left).Within(0.00001));
        Assert.That(canvas.Bottom + canvas.Height * 13 / 64, Is.EqualTo(content.Bottom).Within(0.00001));
        Assert.That(canvas.Right - canvas.Width * 13 / 64, Is.EqualTo(content.Right).Within(0.00001));
        Assert.That(canvas.Top - canvas.Height * 13 / 64, Is.EqualTo(content.Top).Within(0.00001));
    }

    private static bool Covers(BossBeamVertex[] triangles, Vector2 point)
    {
        for (var index = 0; index < triangles.Length; index += 3)
        {
            var a = triangles[index].Position;
            var b = triangles[index + 1].Position;
            var c = triangles[index + 2].Position;
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            if (ab >= -0.00001 && bc >= -0.00001 && ca >= -0.00001 || ab <= 0.00001 && bc <= 0.00001 && ca <= 0.00001)
                return true;
        }
        return false;
    }

    private static float Cross(Vector2 first, Vector2 second) => first.X * second.Y - first.Y * second.X;
}
