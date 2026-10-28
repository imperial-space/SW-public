using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossMovementRuneTest
{
    [TestCase(0, 1, 0)] [TestCase(1, 0, 1)] [TestCase(0, -1, 2)] [TestCase(-1, 0, 3)]
    public void MovingSpritesKeepNativeScaleAndEraseAtBothEnds(int x, int y, int column)
    {
        var axis = new Vector2i(x, y);
        var direction = new Vector2(x, y);
        var path = Enumerable.Range(0, 5).Select(index => axis * index).ToArray();
        var origin = new Vector2(.5f);
        var first = BossMovementRuneVisuals.MovementPatches(path, TimeSpan.FromSeconds(.1)).ToArray();
        var second = BossMovementRuneVisuals.MovementPatches(path, TimeSpan.FromSeconds(.2)).ToArray();
        var moving1 = first.First(patch => Math.Abs(patch.Bounds.Width - .75f) < .00001f && Math.Abs(patch.Bounds.Height - .75f) < .00001f);
        var moving2 = second.First(patch => Math.Abs(patch.Bounds.Width - .75f) < .00001f && Math.Abs(patch.Bounds.Height - .75f) < .00001f);
        Assert.That(Vector2.Dot(moving2.Bounds.Center - moving1.Bounds.Center, direction), Is.EqualTo(.15f).Within(.00001));
        var clipped = false;
        var fading = false;
        for (var sample = 0; sample < 20; sample++)
        {
            var patches = BossMovementRuneVisuals.MovementPatches(path, TimeSpan.FromSeconds(sample * .05)).ToArray();
            Assert.That(patches, Is.Not.Empty);
            foreach (var (bounds, region, opacity) in patches)
            {
                Assert.That(opacity, Is.InRange(float.Epsilon, 1));
                fading |= opacity < 1;
                foreach (var point in new[] { bounds.BottomLeft, bounds.TopRight })
                    Assert.That(Vector2.Dot(point - origin, direction), Is.InRange(-.00001f, 3.00001f),
                        "Arrows must be gone before reaching the enlarged landing rune.");
                Assert.That(region.Width / bounds.Width, Is.EqualTo(32).Within(.0001));
                Assert.That(region.Height / bounds.Height, Is.EqualTo(32).Within(.0001));
                Assert.That(region.Left, Is.GreaterThanOrEqualTo(column * 32 + 4));
                Assert.That(region.Right, Is.LessThanOrEqualTo(column * 32 + 28));
                Assert.That(region.Top, Is.GreaterThanOrEqualTo(4));
                Assert.That(region.Bottom, Is.LessThanOrEqualTo(28));
                clipped |= region.Width < 24 || region.Height < 24;
            }
        }
        Assert.That(clipped, Is.True, "Sprites must erase at the route boundaries rather than overrun them.");
        Assert.That(fading, Is.True, "The endpoints must fade instead of cutting abruptly.");
        var shortened = BossMovementRuneVisuals.MovementPatches(path.Skip(1).ToArray(), TimeSpan.FromSeconds(.1)).ToArray();
        foreach (var patch in first.Where(patch => Vector2.Dot(patch.Bounds.Center - origin, direction) > 1.75f))
            Assert.That(shortened.Any(other => other.Bounds == patch.Bounds && other.Region == patch.Region), Is.True,
                "Route shortening must retain the forward stream phase; its new entrance fades independently.");
        var loop = BossMovementRuneVisuals.MovementPatches(path, TimeSpan.FromSeconds(.1 + 2.0 / 3)).ToArray();
        Assert.That(loop.Length, Is.EqualTo(first.Length));
        for (var index = 0; index < loop.Length; index++)
            Assert.That(Vector2.DistanceSquared(loop[index].Bounds.Center, first[index].Bounds.Center), Is.LessThan(.000001f));
    }

    [Test]
    public void MissingOrNonCardinalRoutesHaveNoRuneStream()
    {
        Assert.That(BossMovementRuneVisuals.MovementPatches([], TimeSpan.Zero), Is.Empty);
        Assert.That(BossMovementRuneVisuals.MovementPatches([Vector2i.Zero], TimeSpan.Zero), Is.Empty);
        Assert.That(BossMovementRuneVisuals.MovementPatches([Vector2i.Zero, Vector2i.Zero], TimeSpan.Zero), Is.Empty);
        Assert.That(BossMovementRuneVisuals.MovementPatches([Vector2i.Zero, new Vector2i(1, 1)], TimeSpan.Zero), Is.Empty);
        Assert.That(BossMovementRuneVisuals.CenteredBounds(new Vector2(3.5f, -2.5f)).Center,
            Is.EqualTo(new Vector2(3.5f, -2.5f)), "The landing rune must mark the exact tile center.");
    }
}
