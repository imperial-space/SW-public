using System.Collections.Generic;
using System.Linq;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossWarningOutlineTest
{
    [TestCase(BossTelegraphPattern.Sinistra)]
    [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)]
    [TestCase(BossTelegraphPattern.Tergum)]
    [TestCase(BossTelegraphPattern.Cross)]
    [TestCase(BossTelegraphPattern.Divide)]
    [TestCase(BossTelegraphPattern.Rejection)]
    [TestCase(BossTelegraphPattern.Invitation)]
    [TestCase(BossTelegraphPattern.Stack)]
    [TestCase(BossTelegraphPattern.CardinalCone)]
    public void OutlineCoversOnlyExposedEdgesWithoutOverdraw(BossTelegraphPattern pattern)
    {
        foreach (var phase in new[] { false, true })
        {
            var danger = BossTilePatterns.Danger(pattern, [], phase);
            var tiles = BossArenaWarningVisuals.Tiles(pattern, danger, 5, 1).ToArray();
            AssertOutline(tiles);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void ClippedConeOutlineFollowsTheRemovedCornerSquaresWithoutGaps(int turns)
    {
        var origin = BossTilePatterns.Rotate(new Vector2i(-7, 0), turns);
        var facing = (1 + turns) % 4;
        var danger = BossTilePatterns.Danger(BossTelegraphPattern.CardinalCone, [], origin: origin, facing: facing);
        AssertOutline(BossArenaWarningVisuals.Tiles(BossTelegraphPattern.CardinalCone, danger, 5, 1, origin, facing).ToArray());
    }

    [Test]
    public void SafeHoleSeparateIslandsAndEmptyWarningsKeepTheirExactContours()
    {
        AssertOutline(BossTilePatterns.GetPlatform(1).Where(t => t != Vector2i.Zero).ToArray());
        AssertOutline([new Vector2i(-5, 5), new Vector2i(5, -5)]);
        AssertOutline([Vector2i.Zero]);
        Assert.That(BossArenaWarningVisuals.Outline([]), Is.Empty);
        Assert.That(new BossTelegraphStylePrototype().WarningAreaOutline, Is.False);
    }

    private static void AssertOutline(IReadOnlyCollection<Vector2i> tiles)
    {
        var area = tiles.ToHashSet();
        var expected = new HashSet<Vector2i>();
        foreach (var tile in area)
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 32; x++)
            if (x < 2 && !area.Contains(tile + new Vector2i(-1, 0)) ||
                x >= 30 && !area.Contains(tile + new Vector2i(1, 0)) ||
                y < 2 && !area.Contains(tile + new Vector2i(0, -1)) ||
                y >= 30 && !area.Contains(tile + new Vector2i(0, 1)))
                expected.Add(new Vector2i(tile.X * 32 + x, tile.Y * 32 + y));

        var actual = new HashSet<Vector2i>();
        foreach (var edge in BossArenaWarningVisuals.Outline(tiles))
        {
            foreach (var value in new[] { edge.Left, edge.Right, edge.Top, edge.Bottom })
                Assert.That(value * 32, Is.EqualTo(MathF.Round(value * 32)), "Contour stays on the native pixel grid.");
            for (var y = (int)(edge.Bottom * 32); y < edge.Top * 32; y++)
            for (var x = (int)(edge.Left * 32); x < edge.Right * 32; x++)
                Assert.That(actual.Add(new Vector2i(x, y)), Is.True, "Corner pixels must not be drawn twice.");
        }
        Assert.That(actual, Is.EquivalentTo(expected), "No shared tile edges, gaps or pixels outside the warning.");
    }
}
