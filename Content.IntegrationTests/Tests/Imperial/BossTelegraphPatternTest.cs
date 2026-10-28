using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossTelegraphPatternTest
{
    [TestCase(BossTelegraphPattern.Sinistra, 66)]
    [TestCase(BossTelegraphPattern.Dextra, 66)]
    [TestCase(BossTelegraphPattern.Frons, 66)]
    [TestCase(BossTelegraphPattern.Tergum, 66)]
    [TestCase(BossTelegraphPattern.BlackoutNorthWest, 112)]
    [TestCase(BossTelegraphPattern.BlackoutNorthEast, 112)]
    [TestCase(BossTelegraphPattern.BlackoutSouthWest, 112)]
    [TestCase(BossTelegraphPattern.BlackoutSouthEast, 112)]
    [TestCase(BossTelegraphPattern.Rejection, 49)]
    [TestCase(BossTelegraphPattern.Invitation, 92)]
    [TestCase(BossTelegraphPattern.Cross, 57)]
    [TestCase(BossTelegraphPattern.Divide, 64)]
    [TestCase(BossTelegraphPattern.BlueFirst, 96)]
    [TestCase(BossTelegraphPattern.PurpleFirst, 85)]
    public void PatternsAreDistinctWholeTilesInsidePlatform(BossTelegraphPattern pattern, int count)
    {
        var tiles = BossTilePatterns.Danger(pattern, Array.Empty<Vector2i>());
        Assert.That(tiles.Count, Is.EqualTo(count));
        Assert.That(tiles.Distinct().Count(), Is.EqualTo(count));
        Assert.That(tiles.All(BossTilePatterns.IsPlatform), Is.True);
    }

    [Test]
    public void EmbraceOverlapsAndConstellationUsesCrossingFullLengthBeams()
    {
        var first = BossTilePatterns.Danger(BossTelegraphPattern.Rejection, Array.Empty<Vector2i>());
        var second = BossTilePatterns.Danger(BossTelegraphPattern.Rejection, Array.Empty<Vector2i>(), true);
        Assert.That(first.Intersect(second).Count(), Is.EqualTo(20));
        Assert.That(first.Union(second).Count(), Is.EqualTo(121));
        var blue = BossTilePatterns.Danger(BossTelegraphPattern.BlueFirst, Array.Empty<Vector2i>());
        var purple = BossTilePatterns.Danger(BossTelegraphPattern.BlueFirst, Array.Empty<Vector2i>(), true);
        Assert.That(blue.Intersect(purple).Count(), Is.EqualTo(60), "Cross-color beam intersections can hit in both phases.");
        Assert.That(blue.Union(purple).Count(), Is.EqualTo(121), "There is no pocket safe through both hits.");
        foreach (var color in new[] { true, false })
        {
            var danger = color ? blue : purple;
            Assert.That(BossTilePatterns.ConstellationBeams(color).Length, Is.EqualTo(3), "Three bands on each axis.");
            foreach (var lane in BossTilePatterns.ConstellationLanes(color))
            foreach (var along in Enumerable.Range(-5, 11))
            {
                Assert.That(danger, Does.Contain(new Vector2i(lane, along)));
                Assert.That(danger, Does.Contain(new Vector2i(along, lane)));
            }
            foreach (var tile in BossTilePatterns.Platform)
                Assert.That(danger.Contains(tile), Is.EqualTo(BossTilePatterns.ConstellationHits(tile, color)));
        }
        Assert.That(blue, Does.Not.Contain(new Vector2i(-2, -2)), "The gap between the first beams is safe.");
        Assert.That(purple, Does.Contain(new Vector2i(-2, -2)), "The second beams fill the previous gap.");
        Assert.That(purple, Does.Not.Contain(new Vector2i(0, 0)));
        foreach (var tile in BossTilePatterns.Platform.Except(blue)) Assert.That(purple, Does.Contain(tile));
        foreach (var tile in BossTilePatterns.Platform.Except(purple)) Assert.That(blue, Does.Contain(tile));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.PurpleFirst, Array.Empty<Vector2i>()), Is.EquivalentTo(purple));
    }

    [Test]
    public void SpreadHasHollowCenterButOverlapsRemoveSafetyAndEdgesClip()
    {
        var center = new Vector2i(0, 0);
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Spread, new[] { center }).Count, Is.EqualTo(24));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Spread, new[] { center }), Does.Not.Contain(center));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Spread, new[] { center, center }), Does.Contain(center));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Spread, new[] { center, new Vector2i(1, 0) }), Does.Contain(center));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Spread, new[] { new Vector2i(5, 5) }).Count, Is.EqualTo(8));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Stack, new[] { center }).Count, Is.EqualTo(112));
        Assert.That(BossTilePatterns.Danger(BossTelegraphPattern.Stack, Array.Empty<Vector2i>()), Is.Empty);
    }

    [Test]
    public void FacingRotatesAlreadyChosenHalfWithoutChangingItsSize()
    {
        var north = BossTilePatterns.Danger(BossTelegraphPattern.Frons, Array.Empty<Vector2i>());
        var west = BossTilePatterns.Danger(BossTelegraphPattern.Frons, Array.Empty<Vector2i>(), facing: 1);
        Assert.That(west, Is.EquivalentTo(north.Select(tile => BossTilePatterns.Rotate(tile, 1))));
        Assert.That(west.All(tile => tile.X <= 0), Is.True);
    }
}
