using System.IO;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
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
public sealed class RevenantAutoProjectileAssetTest
{
    [Test]
    public async Task AutoProjectileNativeAssetsKeepApprovedPixelsAndSlowerSpin()
    {
        var resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(PoolManager).Assembly.Location)!, "../../Resources"));
        using var engine = new RobustIntegrationTest.ServerIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new()
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                MountOptions = new() { DirMounts = [resources] },
                PrototypeDirectory = new("/AutoNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/auto_flame.rsi");
            foreach (var (name, hash) in new[]
            {
                ("flight", "72A0B9C6480D54453F8311C73ACAACA630A3EDA2478EA8E679EC3D32C03349DA"),
                ("impact", "15263A002E7E5ECDDD3B75A352D580603C5D9CEFCB8B08E4B137671CF938E5F9")
            })
            {
                using var stream = manager.ContentFileRead(path / (name + ".png"));
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)), Is.EqualTo(hash), "Use the approved pixels.");
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
                Assert.That(parameters.SampleParameters.Filter, Is.False);
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { 12, 5, 12, 12 }));
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(192, 160)));
                Assert.That(rsi["flight"].AnimationLength, Is.EqualTo((float)BossAutoProjectileVisuals.FlightLoopSeconds).Within(.00001));
                Assert.That(rsi["impact"].AnimationLength, Is.EqualTo(.52f).Within(.00001));
                Assert.That(rsi["charge"].AnimationLength, Is.EqualTo(.6f).Within(.00001));
                Assert.That(rsi["trail"].AnimationLength, Is.EqualTo(1.2f).Within(.00001));
                var first = 0;
                foreach (var (name, count) in new[] { ("flight", 12), ("impact", 5), ("charge", 12), ("trail", 12) })
                {
                    using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / (name + ".png")));
                    for (var frame = 0; frame < count; frame++)
                    for (var y = 0; y < 160; y++)
                    for (var x = 0; x < 192; x++)
                    {
                        var pixel = source[frame % 4 * 192 + x, frame / 4 * 160 + y];
                        if (atlas[(first + frame) % columns * 192 + x, (first + frame) / columns * 160 + y] != pixel)
                            Assert.Fail($"Native loader changed {name} frame {frame} at {x},{y}.");
                        if (source[frame % 4 * 192 + x / 2 * 2, frame / 4 * 160 + y / 2 * 2] != pixel)
                            Assert.Fail("Keep a consistent 2x2 pixel grid.");
                        if (name == "impact" && frame == 4) Assert.That(pixel.A, Is.Zero);
                    }
                    first += count;
                }
            }
            finally
            {
                foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
                Configuration.Default.PreferContiguousImageBuffers = previous;
            }
        });
    }
}
