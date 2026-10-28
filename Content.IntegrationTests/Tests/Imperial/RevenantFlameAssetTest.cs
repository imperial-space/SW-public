using System.IO;
using System.Reflection;
using System.Security.Cryptography;
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
public sealed class RevenantFlameAssetTest
{
    [Test]
    public async Task NativeFlameLoaderKeepsBothApprovedColorsAllFramesAndMatchingTiming()
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
                PrototypeDirectory = new("/FlameNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Structures/RevenantBossConstellation/flame.rsi");
            var names = new[] { "blue", "purple" };
            var hashes = new[] { "35BDF516FCA5C8DC3AA584939D859DF2F104689789AE24E9A2E2B2D57E8D6DFA",
                "B19ACF54CE8A37D7037F4AA7A9032DC114961E0FDA45ACAADB1AB955AF4566D0" };
            for (var i = 0; i < names.Length; i++)
            {
                using var stream = manager.ContentFileRead(path / (names[i] + ".png"));
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)), Is.EqualTo(hashes[i]), "Use the exact approved flame pixels.");
            }
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
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { 32, 32 }));
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(64, 64)));
                for (var color = 0; color < names.Length; color++)
                {
                    Assert.That(rsi.TryGetState(names[color], out var state), Is.True);
                    Assert.That(state!.DelayCount, Is.EqualTo(32));
                    Assert.That(state.AnimationLength, Is.EqualTo(3.2f).Within(0.00001));
                    using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / (names[color] + ".png")));
                    for (var frame = 0; frame < 32; frame++)
                    {
                        Assert.That(state.GetDelay(frame), Is.EqualTo(0.1f));
                        var index = color * 32 + frame;
                        for (var y = 0; y < 64; y++)
                        for (var x = 0; x < 64; x++)
                            if (atlas[index % columns * 64 + x, index / columns * 64 + y] != source[frame % 4 * 64 + x, frame / 4 * 64 + y])
                                Assert.Fail($"Native loader changed {names[color]} frame {frame} at ({x}, {y}).");
                    }
                }
                for (var frame = 0; frame < 32; frame++)
                for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                    if (atlas[frame % columns * 64 + x, frame / columns * 64 + y].A !=
                        atlas[(32 + frame) % columns * 64 + x, (32 + frame) / columns * 64 + y].A)
                        Assert.Fail($"Both native colors must retain identical flame shapes in frame {frame}.");
            }
            finally
            {
                foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
                Configuration.Default.PreferContiguousImageBuffers = previous;
            }
        });
    }
}
