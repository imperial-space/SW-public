using System.IO;
using System.Linq;
using System.Reflection;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantRainCrystalAssetTest
{
    [TestCase("small", false, 150)]
    [TestCase("large", false, 150)]
    [TestCase("small", true, 60)]
    [TestCase("large", true, 60)]
    public async Task ApprovedRainCrystalFramesLoadWithExactPixelsTimingAndNearestSampling(string kind, bool impact, int frames)
    {
        var resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(PoolManager).Assembly.Location)!, "../../Resources"));
        using var engine = new RobustIntegrationTest.ServerIntegrationInstance(new()
        {
            ContentStart = false,
            LoadTestAssembly = false,
            Options = new()
            {
                LoadContentResources = false,
                LoadConfigAndUserData = false,
                MountOptions = new() { DirMounts = [resources] },
                PrototypeDirectory = new("/RainCrystalNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var directory = "/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/";
            var path = new ResPath(directory + "rain_" + kind + "_" + (impact ? "shatter" : "warning") + ".rsi");
            var previousContiguous = Configuration.Default.PreferContiguousImageBuffers;
            Configuration.Default.PreferContiguousImageBuffers = true;
            var type = typeof(RSIResource).GetNestedType("LoadStepData", BindingFlags.NonPublic)!;
            var step = Activator.CreateInstance(type, nonPublic: true)!;
            type.GetField("Path")!.SetValue(step, path);
            typeof(RSIResource).GetMethod("LoadPreTexture", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [manager, step]);
            using var atlas = (Image<Rgba32>)type.GetField("AtlasSheet")!.GetValue(step)!;
            try
            {
                var rsi = (RSI)type.GetField("Rsi")!.GetValue(step)!;
                var columns = (int)type.GetField("DimX")!.GetValue(step)!;
                var parameters = (TextureLoadParameters)type.GetField("LoadParameters")!.GetValue(step)!;
                Assert.That(parameters.SampleParameters.Filter, Is.False);
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { frames }));
                Assert.That(Math.Max(atlas.Width, atlas.Height), Is.LessThanOrEqualTo(4096));
                Assert.That(rsi.TryGetState("animation", out var state), Is.True);
                Assert.That(state!.DelayCount, Is.EqualTo(frames));
                Assert.That(state.AnimationLength, Is.EqualTo(impact ? 1.2f : 3f).Within(0.0001));
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(160, impact ? 384 : 320)));
                using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / "animation.png"));
                var sourceColumns = source.Width / rsi.Size.X;

                for (var index = 0; index < frames; index++)
                for (var y = 0; y < rsi.Size.Y; y++)
                for (var x = 0; x < rsi.Size.X; x++)
                    if (atlas[index % columns * rsi.Size.X + x, index / columns * rsi.Size.Y + y] !=
                        source[index % sourceColumns * rsi.Size.X + x, index / sourceColumns * rsi.Size.Y + y])
                        Assert.Fail($"Native atlas changed {kind} frame {index} at ({x}, {y}).");
            }
            finally
            {
                foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
                Configuration.Default.PreferContiguousImageBuffers = previousContiguous;
            }
        });
    }

    [TestCase("small")] [TestCase("large")]
    public void ApprovedCrystalReachesTheFloorAndClipsThroughItBeforeTheUpperBodyDisappears(string kind)
    {
        var resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(PoolManager).Assembly.Location)!, "../../Resources"));
        var directory = Path.Combine(resources, "Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss");
        using var warning = Image.Load<Rgba32>(Path.Combine(directory, "rain_" + kind + "_warning.rsi/animation.png"));
        using var impact = Image.Load<Rgba32>(Path.Combine(directory, "rain_" + kind + "_shatter.rsi/animation.png"));
        var lowestWarningPixel = 0;
        var standingPixelsAtContact = 0;
        var standingPixelsAfterDescent = 0;
        for (var y = 0; y < 320; y++)
        for (var x = 0; x < 160; x++)
        {

            if (warning[5 * 160 + x, 9 * 320 + y].A > 0)
                lowestWarningPixel = Math.Max(lowestWarningPixel, y);

            if (y < 224 && impact[x, y].A > 0) standingPixelsAtContact++;
            if (y < 224 && impact[6 * 160 + x, y].A > 0) standingPixelsAfterDescent++;
        }
        Assert.That(lowestWarningPixel, Is.InRange(301, 303), "The tip must reach the floor, allowing original transparent tip padding.");
        Assert.That(standingPixelsAtContact, Is.GreaterThan(0), "The whole crystal cannot disappear before contact.");
        Assert.That(standingPixelsAfterDescent, Is.Zero, "After 0.12 seconds only low fragments remain above the floor.");
    }

    [TestCase(0.5)] [TestCase(3)] [TestCase(10)]
    public void CrystalContactPlaybackContinuesWithoutHoldingOrRestartingOnLateImpact(double duration)
    {
        foreach (var late in new[] { 0, .04, .1, .35, .9, 1.2 })
        {
            var warning = BossRainCrystalVisuals.Playback(duration + late, duration, false);
            var impact = BossRainCrystalVisuals.Playback(late, 1.2, true);
            Assert.That(warning.Impact, Is.True);
            Assert.That(warning.Elapsed, Is.EqualTo(late).Within(1e-7));
            Assert.That(BossRainCrystalVisuals.FrameIndex(warning.Elapsed, duration, warning.Impact),
                Is.EqualTo(BossRainCrystalVisuals.FrameIndex(impact.Elapsed, 1.2, impact.Impact)),
                "Receiving an impact entity must continue the same frame, not start from frame zero.");
        }
        var before = BossRainCrystalVisuals.Playback(duration - .01, duration, false);
        Assert.That(before.Impact, Is.False);
        Assert.That(BossRainCrystalVisuals.FrameIndex(before.Elapsed, duration, false), Is.EqualTo(149));
    }

    [TestCase(0.5)] [TestCase(3)] [TestCase(10)]
    public void RainCrystalFallMeetsTheNativeDeadlineForShortNormalAndLongWarnings(double duration)
    {
        Assert.That(BossRainCrystalVisuals.FrameIndex(0, duration, false), Is.Zero);
        Assert.That(BossRainCrystalVisuals.WarningPlayback(duration - 0.16, duration), Is.EqualTo(2.84).Within(1e-7));
        Assert.That(BossRainCrystalVisuals.FrameIndex(duration - 0.01, duration, false), Is.EqualTo(149));
        Assert.That(BossRainCrystalVisuals.FrameIndex(duration, duration, false), Is.EqualTo(149));
        if (duration == 3)
            for (var index = 0; index < 150; index++)
                Assert.That(BossRainCrystalVisuals.FrameIndex((index + 0.5) * 0.02, duration, false), Is.EqualTo(index));
        for (var index = 0; index < 60; index++)
            Assert.That(BossRainCrystalVisuals.FrameIndex((index + 0.5) * 0.02, 1.2, true), Is.EqualTo(index));
    }
}
