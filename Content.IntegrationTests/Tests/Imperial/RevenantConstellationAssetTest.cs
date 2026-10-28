using System.IO;
using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using System.Reflection;
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
public sealed class RevenantConstellationAssetTest
{
    [Test]
    public async Task NativeSingleBeamLoaderKeepsRefinedPixelsAndTheCompleteFade()
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
                PrototypeDirectory = new("/BeamNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Effects/Boss/flame_beam.rsi");
            var names = new[] { "blue-1", "blue-2", "purple-1", "purple-2" };
            var previous = Configuration.Default.PreferContiguousImageBuffers;
            Configuration.Default.PreferContiguousImageBuffers = true;
            var type = typeof(RSIResource).GetNestedType("LoadStepData", BindingFlags.NonPublic)!;
            var step = Activator.CreateInstance(type, nonPublic: true)!;
            type.GetField("Path")!.SetValue(step, path);
            typeof(RSIResource).GetMethod("LoadPreTexture", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [manager, step]);
            using var atlas = (Image<Rgba32>)type.GetField("AtlasSheet")!.GetValue(step)!;
            try
            {
                var rsi = (RSI)type.GetField("Rsi")!.GetValue(step)!;
                var columns = (int)type.GetField("DimX")!.GetValue(step)!;
                var parameters = (TextureLoadParameters)type.GetField("LoadParameters")!.GetValue(step)!;
                Assert.That(parameters.SampleParameters.Filter, Is.False, "Retain the pixel grid.");
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { 58, 58, 58, 58 }));
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(256, 64)));
                for (var color = 0; color < names.Length; color++)
                {
                    Assert.That(rsi.TryGetState(names[color], out var state), Is.True);
                    Assert.That(state!.DelayCount, Is.EqualTo(58));
                    Assert.That(state.AnimationLength, Is.EqualTo(1.74f).Within(0.00001));
                    using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / (names[color] + ".png")));
                    for (var frame = 0; frame < 58; frame++)
                    {
                        Assert.That(state.GetDelay(frame), Is.EqualTo(0.03f));
                        var index = color * 58 + frame;
                        for (var y = 0; y < 64; y++)
                        for (var x = 0; x < 256; x++)
                            if (atlas[index % columns * 256 + x, index / columns * 64 + y] != source[frame % 8 * 256 + x, frame / 8 * 64 + y])
                                Assert.Fail($"Native beam loader changed {names[color]} frame {frame} at ({x}, {y}).");
                    }
                }
            }
            finally
            {
                foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
                Configuration.Default.PreferContiguousImageBuffers = previous;
            }
        });
    }

    [Test]
    public void ConstellationPlaybackUsesTheCorrectColorPhaseAndIndependentLines()
    {
        Assert.That(BossConstellationVisuals.State(BossTelegraphPattern.BlueFirst, false), Is.EqualTo("blue"));
        Assert.That(BossConstellationVisuals.State(BossTelegraphPattern.BlueFirst, true), Is.EqualTo("purple"));
        Assert.That(BossConstellationVisuals.State(BossTelegraphPattern.PurpleFirst, false), Is.EqualTo("purple"));
        Assert.That(BossConstellationVisuals.State(BossTelegraphPattern.PurpleFirst, true), Is.EqualTo("blue"));
        for (var frame = 0; frame < 58; frame++)
            Assert.That(BossConstellationVisuals.FrameIndex((frame + .5) * .03), Is.EqualTo(frame));
        Assert.That(BossConstellationVisuals.FrameIndex(1.74), Is.EqualTo(57));
        Assert.That(BossConstellationVisuals.FrameIndex(double.NaN), Is.Zero);
        var beams = BossConstellationVisuals.Beams(BossTelegraphPattern.BlueFirst, false, 5).ToArray();
        Assert.That(beams, Has.Length.GreaterThan(2));
        Assert.That(beams[0].Start.X, Is.EqualTo(-5.625f));
        Assert.That(beams[0].End.X, Is.EqualTo(6.625f));
        Assert.That(beams[0].Start.Y, Is.EqualTo(beams[0].End.Y));
        Assert.That(beams[1].Start.X, Is.EqualTo(beams[1].End.X));

    }
}
