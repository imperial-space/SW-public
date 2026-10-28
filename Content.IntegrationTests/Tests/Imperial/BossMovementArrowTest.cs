using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Boss;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossMovementArrowTest
{
    [TestCase(0, 1)] [TestCase(1, 0)] [TestCase(0, -1)] [TestCase(-1, 0)]
    public void FlowMovesAtConstantSpeedClipsAtDestinationAndSurvivesRouteShortening(int x, int y)
    {
        var axis = new Vector2i(x, y);
        var direction = new Vector2(x, y);
        var origin = new Vector2(0.5f);
        var path = Enumerable.Range(0, 5).Select(i => axis * i).ToList();
        var first = BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(0.1)).ToArray();
        var second = BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(0.2)).ToArray();
        var tip1 = first.First(line => Vector2.Distance(line.Start, line.End) > 0.54f).Start;
        var tip2 = second.First(line => Vector2.Distance(line.Start, line.End) > 0.54f).Start;
        Assert.That(Vector2.Dot(tip2 - tip1, direction), Is.EqualTo(0.15f).Within(0.00001));
        for (var sample = 0; sample < 15; sample++)
        {
            var lines = BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(sample * 0.1)).ToArray();
            Assert.That(lines, Is.Not.Empty);
            foreach (var point in lines.SelectMany(line => new[] { line.Start, line.End }))
                Assert.That(Vector2.Dot(point - origin, direction), Is.InRange(-0.00001f, 4.00001f));
        }
        var shortened = BossFloorTelegraphOverlay.MovementArrowLines(path.Skip(1).ToArray(), TimeSpan.FromSeconds(0.1)).ToArray();
        var retained = first.Where(line => Vector2.Dot(line.Start - origin, direction) > 1 &&
            Vector2.Dot(line.End - origin, direction) > 1).ToArray();
        foreach (var line in retained)
            Assert.That(shortened.Any(other => Vector2.DistanceSquared(line.Start, other.Start) < 0.000001f &&
                Vector2.DistanceSquared(line.End, other.End) < 0.000001f), Is.True);
        var vanishing = BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(0.3)).ToArray();
        var clippedShaft = vanishing.First();
        Assert.That(Vector2.Dot(clippedShaft.Start - origin, direction), Is.EqualTo(4).Within(0.00001));
        Assert.That(Vector2.Distance(clippedShaft.Start, clippedShaft.End), Is.EqualTo(0.1f).Within(0.00001));
        Assert.That(BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(0.4)).All(line =>
            Vector2.Dot(line.Start - origin, direction) < 4), Is.True);
        var loop = BossFloorTelegraphOverlay.MovementArrowLines(path, TimeSpan.FromSeconds(0.1 + 2.0 / 3)).ToArray();
        Assert.That(loop.Length, Is.EqualTo(first.Length));
        for (var i = 0; i < first.Length; i++)
        {
            Assert.That(Vector2.DistanceSquared(loop[i].Start, first[i].Start), Is.LessThan(0.000001f));
            Assert.That(Vector2.DistanceSquared(loop[i].End, first[i].End), Is.LessThan(0.000001f));
        }
        Assert.That(BossFloorTelegraphOverlay.MovementArrowLines([], TimeSpan.Zero), Is.Empty);
        Assert.That(BossFloorTelegraphOverlay.MovementArrowLines([Vector2i.Zero], TimeSpan.Zero), Is.Empty);
    }
}
