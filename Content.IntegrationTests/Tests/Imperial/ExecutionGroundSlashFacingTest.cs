using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class ExecutionGroundSlashFacingTest
{
    [TestCase(0f, RsiDirection.East)]
    [TestCase(MathF.PI / 4, RsiDirection.NorthEast)]
    [TestCase(MathF.PI / 2, RsiDirection.North)]
    [TestCase(3 * MathF.PI / 4, RsiDirection.NorthWest)]
    [TestCase(MathF.PI, RsiDirection.West)]
    [TestCase(-3 * MathF.PI / 4, RsiDirection.SouthWest)]
    [TestCase(-MathF.PI / 2, RsiDirection.South)]
    [TestCase(-MathF.PI / 4, RsiDirection.SouthEast)]
    public void CompassProfilesKeepTheirLeadingPointOnTheLine(float angle, RsiDirection expected)
    {
        Assert.That(BossExecutionLineVisuals.TipDirection(angle), Is.EqualTo(expected));
        var bounds = BossExecutionLineVisuals.TipBounds(expected, new Vector2i(64, 64));
        var apex = BossExecutionLineVisuals.TipAnchorPixels(expected);
        var leadingPoint = new Vector2(bounds.Left + apex.X / 32, bounds.Top - apex.Y / 32);
        Assert.That(leadingPoint.Length(), Is.LessThan(.000001));
        Assert.That(MathF.Abs(BossExecutionLineVisuals.TipResidualAngle(angle, expected)), Is.LessThan(.000001));
    }

    [TestCase(RsiDirection.East, 120, 64)] [TestCase(RsiDirection.NorthEast, 100, 28)]
    [TestCase(RsiDirection.North, 64, 7)] [TestCase(RsiDirection.NorthWest, 28, 27)]
    [TestCase(RsiDirection.West, 7, 63)] [TestCase(RsiDirection.SouthWest, 27, 99)]
    [TestCase(RsiDirection.South, 63, 120)] [TestCase(RsiDirection.SouthEast, 99, 100)]
    public void ApprovedThreeBladeCrestKeepsItsContactPointAndNativePixelScale(RsiDirection direction, int x, int y)
    {
        var size = new Vector2i(128, 128);
        var bounds = BossExecutionLineVisuals.TipBounds(direction, size);
        Assert.That(BossExecutionLineVisuals.TipAnchorPixels(direction, size), Is.EqualTo(new Vector2(x, y)));
        Assert.That(new Vector2(bounds.Left + x / 32f, bounds.Top - y / 32f).Length(), Is.LessThan(.000001));
        Assert.That(bounds.Width, Is.EqualTo(4));
        Assert.That(bounds.Height, Is.EqualTo(4));
    }

    [TestCase(.2f)] [TestCase(.6f)] [TestCase(1.3f)] [TestCase(-2.5f)]
    [TestCase(-3f)] [TestCase(MathF.PI + .1f)]
    public void IntermediateAimAndWrapPreserveTheNativeProfileDirection(float angle)
    {
        var facing = BossExecutionLineVisuals.TipDirection(angle);
        var residual = BossExecutionLineVisuals.TipResidualAngle(angle, facing);
        Assert.That(MathF.Abs(residual), Is.LessThanOrEqualTo(MathF.PI / 8 + .000001));
        var reconstructed = BossExecutionLineVisuals.TipAngle(facing) + residual;
        var actual = new Vector2(MathF.Cos(reconstructed), MathF.Sin(reconstructed));
        var expected = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.000001));
    }

    [TestCase(0f)] [TestCase(37f)] [TestCase(90f)]
    [TestCase(180f)] [TestCase(270f)] [TestCase(-53f)]
    public void ViewportProjectionChoosesNearestProfileAndKeepsBladesOnTheLine(float cameraDegrees)
    {
        var eye = new Eye { Rotation = Angle.FromDegrees(cameraDegrees) };
        eye.GetViewMatrix(out var view, Vector2.One);
        var camera = (float)eye.Rotation;
        var targets = new (float Degrees, RsiDirection Facing)[]
        {
            (0, RsiDirection.East), (45, RsiDirection.NorthEast),
            (90, RsiDirection.North), (135, RsiDirection.NorthWest),
            (180, RsiDirection.West), (225, RsiDirection.SouthWest),
            (270, RsiDirection.South), (315, RsiDirection.SouthEast),
            (20, RsiDirection.East), (25, RsiDirection.NorthEast),
            (200, RsiDirection.West), (205, RsiDirection.SouthWest),
            (350, RsiDirection.East), (-25, RsiDirection.SouthEast),
        };
        foreach (var gridDegrees in new[] { 0f, 37f, 90f, -53f })
        foreach (var (degrees, expected) in targets)
        {
            var gridAngle = (float)Angle.FromDegrees(gridDegrees);
            var grid = Matrix3x2.CreateRotation(gridAngle) * Matrix3x2.CreateTranslation(8, -4);
            var localAngle = (float)Angle.FromDegrees(degrees) - camera - gridAngle;
            var localDirection = new Vector2(MathF.Cos(localAngle), MathF.Sin(localAngle));
            var worldDirection = Vector2.TransformNormal(localDirection, grid);
            var (facing, rotation) = BossExecutionLineVisuals.TipPose(worldDirection, camera);
            Assert.That(facing, Is.EqualTo(expected), $"camera {cameraDegrees}, grid {gridDegrees}, screen {degrees}");
            var endpoint = Vector2.Transform(localDirection * 6, grid);
            var model = Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(endpoint);
            var nativeAngle = BossExecutionLineVisuals.TipAngle(facing);
            var nativeForward = new Vector2(MathF.Cos(nativeAngle), MathF.Sin(nativeAngle));
            var rendered = Vector2.Normalize(Vector2.TransformNormal(nativeForward, model * view));
            var aimed = Vector2.Normalize(Vector2.TransformNormal(localDirection, grid * view));
            Assert.That(Vector2.Distance(rendered, aimed), Is.LessThan(.00001), "Blade direction must match the projected damage line.");
            Assert.That(MathF.Abs(BossExecutionLineVisuals.TipResidualAngle((float)Angle.FromDegrees(degrees), facing)),
                Is.LessThanOrEqualTo(MathF.PI / 8 + .00001), "Use the nearest native profile to minimize pixel rotation.");
            var size = new Vector2i(128, 128);
            var bounds = BossExecutionLineVisuals.TipBounds(facing, size);
            var anchor = BossExecutionLineVisuals.TipAnchorPixels(facing, size);
            var contact = new Vector2(bounds.Left + anchor.X / 32f, bounds.Top - anchor.Y / 32f);
            Assert.That(Vector2.Distance(Vector2.Transform(contact, model * view), Vector2.Transform(endpoint, view)),
                Is.LessThan(.00001), "Changing profiles must not move the leading blade off the line endpoint.");
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void DirectionSectorsRemainCorrectOnBothSidesOfEveryBoundary(int octant)
    {
        var before = new[] { RsiDirection.East, RsiDirection.NorthEast, RsiDirection.North, RsiDirection.NorthWest,
            RsiDirection.West, RsiDirection.SouthWest, RsiDirection.South, RsiDirection.SouthEast };
        var boundary = octant * MathF.PI / 4 + MathF.PI / 8;
        foreach (var turn in new[] { -2, -1, 0, 1, 2 })
        {
            Assert.That(BossExecutionLineVisuals.TipDirection(boundary - .0001f + turn * MathF.Tau), Is.EqualTo(before[octant]));
            Assert.That(BossExecutionLineVisuals.TipDirection(boundary + .0001f + turn * MathF.Tau), Is.EqualTo(before[(octant + 1) % 8]));
        }
    }

    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
    public void InvalidAnglesHaveAnExplicitSafeProfile(float angle)
        => Assert.That(BossExecutionLineVisuals.TipDirection(angle), Is.EqualTo(RsiDirection.East));
}
