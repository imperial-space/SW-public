using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossArenaWarningOverflowTest
{
    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    [TestCase(BossTelegraphPattern.Rejection)] [TestCase(BossTelegraphPattern.Invitation)]
    [TestCase(BossTelegraphPattern.Cross)] [TestCase(BossTelegraphPattern.Divide)]
    [TestCase(BossTelegraphPattern.CardinalCone)] [TestCase(BossTelegraphPattern.Stack)]
    [TestCase(BossTelegraphPattern.BlueFirst)] [TestCase(BossTelegraphPattern.PurpleFirst)]
    [TestCase(BossTelegraphPattern.BlackoutNorthWest)] [TestCase(BossTelegraphPattern.BlackoutNorthEast)]
    [TestCase(BossTelegraphPattern.BlackoutSouthWest)] [TestCase(BossTelegraphPattern.BlackoutSouthEast)]
    public void OnlyTheOuterRingGrowsAndAuthoritativeFloorCellsStayIdentical(BossTelegraphPattern pattern)
    {
        foreach (var phase in new[] { false, true })
        foreach (var origin in new[] { Vector2i.Zero, new Vector2i(4, 0), new Vector2i(0, 7) })
        {
            var danger = BossTilePatterns.Danger(pattern, [new Vector2i(4, 4)], phase, origin: origin);
            var snapshot = danger.ToArray();
            var rendered = BossArenaWarningVisuals.Tiles(pattern, danger, 5, 1, origin).ToArray();
            Assert.That(danger, Is.EqualTo(snapshot), "Rendering must never mutate the damage mask.");
            Assert.That(rendered.Distinct().Count(), Is.EqualTo(rendered.Length));
            Assert.That(rendered.Where(tile => BossTilePatterns.IsPlatform(tile)), Is.EquivalentTo(danger));
            foreach (var tile in rendered)
            {
                Assert.That(Math.Abs(tile.X), Is.LessThanOrEqualTo(6));
                Assert.That(Math.Abs(tile.Y), Is.LessThanOrEqualTo(6));
                if (BossTilePatterns.IsPlatform(tile)) continue;
                Assert.That(danger, Does.Contain(new Vector2i(Math.Clamp(tile.X, -5, 5), Math.Clamp(tile.Y, -5, 5))));
            }
            foreach (var tile in BossTilePatterns.GetPlatform(6).Where(t => !BossTilePatterns.IsPlatform(t)))
            {
                var expected = danger.Contains(new Vector2i(Math.Clamp(tile.X, -5, 5), Math.Clamp(tile.Y, -5, 5)));
                if (pattern == BossTelegraphPattern.CardinalCone)
                    expected &= BossTilePatterns.Danger(pattern, [], origin: origin, halfSize: 6).Contains(tile);
                Assert.That(rendered.Contains(tile), Is.EqualTo(expected), "One tile of edge coverage following the cone's diagonal.");
            }
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void DisplacedConeDropsCornerProtrusionsWhileKeepingItsOuterStrip(int turns)
    {
        var origin = BossTilePatterns.Rotate(new Vector2i(-7, 0), turns);
        var facing = (1 + turns) % 4;
        var danger = BossTilePatterns.Danger(BossTelegraphPattern.CardinalCone, [], origin: origin, facing: facing);
        var rendered = BossArenaWarningVisuals.Tiles(BossTelegraphPattern.CardinalCone, danger, 5, 1, origin, facing).ToArray();

        foreach (var corner in new[] { new Vector2i(-6, -2), new(-2, -6), new(-6, 2), new(-2, 6) })
            Assert.That(rendered, Does.Not.Contain(BossTilePatterns.Rotate(corner, turns)));
        foreach (var strip in new[] { new Vector2i(-6, -1), new(-1, -6), new(-6, 1), new(-1, 6) })
            Assert.That(rendered, Does.Contain(BossTilePatterns.Rotate(strip, turns)), "Keep the rest of the one-tile extension.");
        Assert.That(rendered.Where(tile => BossTilePatterns.IsPlatform(tile)), Is.EquivalentTo(danger), "Never remove a warning from an actual damage tile.");
        var previous = BossTilePatterns.GetPlatform(6).Where(tile => danger.Contains(
            new Vector2i(Math.Clamp(tile.X, -5, 5), Math.Clamp(tile.Y, -5, 5)))).ToArray();
        Assert.That(previous.Except(rendered).Count(), Is.EqualTo(4), "Only the four equivalent sloped-end protrusions disappear.");
        Assert.That(rendered.Except(previous), Is.Empty);
    }

    [TestCase(BossTelegraphPattern.Sinistra, 91)] [TestCase(BossTelegraphPattern.Dextra, 91)]
    [TestCase(BossTelegraphPattern.Frons, 91)] [TestCase(BossTelegraphPattern.Tergum, 91)]
    [TestCase(BossTelegraphPattern.Cross, 69)] [TestCase(BossTelegraphPattern.Divide, 100)]
    public void ArenaSpanningPatternsReachOneExtraTileWithoutWideningTheirInternalLanes(BossTelegraphPattern pattern, int count)
    {
        var danger = BossTilePatterns.Danger(pattern, []);
        var rendered = BossArenaWarningVisuals.Tiles(pattern, danger, 5, 1).ToArray();
        Assert.That(rendered, Has.Length.EqualTo(count));
        if (pattern == BossTelegraphPattern.Cross)
        {
            Assert.That(rendered.Where(t => t.X == 6).Select(t => t.Y), Is.EquivalentTo(new[] { -1, 0, 1 }));
            Assert.That(rendered.Where(t => t.Y == 6).Select(t => t.X), Is.EquivalentTo(new[] { -1, 0, 1 }));
        }
        foreach (var degrees in new[] { 0, 90, 180, 270 })
        {
            var rotated = rendered.Select(t => Angle.FromDegrees(degrees).RotateVec(new Vector2(t.X, t.Y)));
            Assert.That(rotated.All(p => MathF.Abs(p.X) <= 6.00001f && MathF.Abs(p.Y) <= 6.00001f), Is.True);
        }
    }

    [TestCase(BossTelegraphPattern.Spread)] [TestCase(BossTelegraphPattern.CrystalRain)]
    [TestCase(BossTelegraphPattern.CrystalRainSmall)] [TestCase(BossTelegraphPattern.TravelOnly)]
    public void TargetedEffectsAndOtherBossStylesKeepTheirExistingFootprints(BossTelegraphPattern pattern)
    {
        var danger = BossTilePatterns.GetPlatform(5);
        Assert.That(BossArenaWarningVisuals.Tiles(pattern, danger, 5, 1), Is.EqualTo(danger));
        Assert.That(BossArenaWarningVisuals.Tiles(BossTelegraphPattern.Cross, danger, 5, 0), Is.EqualTo(danger));
        Assert.That(new BossTelegraphStylePrototype().WarningOverflowTiles, Is.Zero);
        Assert.That(BossArenaWarningVisuals.Tiles(BossTelegraphPattern.Cross, [], 5, 1), Is.Empty);
    }

    [Test]
    public void AFullArenaWarningCoversThirteenByThirteenAndInnerBurstsDoNotGrow()
    {
        var full = BossArenaWarningVisuals.Tiles(BossTelegraphPattern.Stack, BossTilePatterns.GetPlatform(5), 5, 1).ToArray();
        Assert.That(full, Is.EquivalentTo(BossTilePatterns.GetPlatform(6)));
        var inner = BossTilePatterns.Danger(BossTelegraphPattern.Rejection, []);
        Assert.That(BossArenaWarningVisuals.Tiles(BossTelegraphPattern.Rejection, inner, 5, 1), Is.EqualTo(inner));
    }

    [Test]
    public void AimedWarningsAndSlashEndpointsReachTheOuterBoundaryWithoutChangingDamageLength()
    {
        var origin = new Vector2(.5f);
        for (var degrees = 0; degrees < 360; degrees++)
        {
            var direction = Angle.FromDegrees(degrees).RotateVec(Vector2.UnitX);
            var damageLength = BossExecutionLineVisuals.LengthToEdge(origin, direction, 5);
            var visualLength = BossArenaWarningVisuals.LineLength(origin, direction, damageLength, 5, 1);
            var endpoint = origin + direction * visualLength;
            Assert.That(MathF.Max(MathF.Abs(endpoint.X - .5f), MathF.Abs(endpoint.Y - .5f)), Is.EqualTo(6.5f).Within(.00002));
            Assert.That(BossArenaWarningVisuals.LineLength(origin, direction, damageLength, 5, 0), Is.EqualTo(damageLength));
            Assert.That(BossExecutionLineVisuals.Contains(endpoint, origin, direction, damageLength, 3), Is.False);
        }
    }
}
