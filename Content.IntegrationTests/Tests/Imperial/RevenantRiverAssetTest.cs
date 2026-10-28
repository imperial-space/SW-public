using System.IO;
using System.Linq;
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
public sealed class RevenantRiverAssetTest
{
    [TestCase("cross_rivers", "AD13B4A44D78B585D0BE2DC86AE837C5134926AEDA8689DE131F725F774E251C")]
    [TestCase("divide_rivers", "3569D0D33BA86E62F89FDC0D6F0F5B297DF372533317276D2AD5DD033F69692B")]
    [TestCase("cross_rivers_north", "1D903EEBFECA77C987C152C9D826F2C213173EE3FE4531B7782E1CBF39849A56")]
    [TestCase("cross_rivers_east", "68E68D10A76E7A9069D0CE6F44F75BF06AAD409F681CD7129AEC90E18187A508")]
    [TestCase("cross_rivers_south", "78B1C587082E934D5616902D1E73B796317ED6EB6C0525D40061597D20942D64")]
    [TestCase("cross_rivers_west", "CBD3B58C77F64069AE7B19B6F4ABF634EA11F372414225946BB54F1ADA9B64F3")]
    [TestCase("cone_rivers_north", "E42B3A8E378A6F2CBD9BC89F5CEDE162D108DA2ADBEDA835F479FE537A59F260")]
    [TestCase("cone_rivers_east", "3D2F33E025C19708657376D80DAD34FDB050E0235EC70646CD4FF87012900C56")]
    [TestCase("cone_rivers_south", "8253D7B81E5C1D43E3391BAF1368C088DCD92267B9474E1FDDB7B251039730B3")]
    [TestCase("cone_rivers_west", "31C869A8D58B907B3605FD64948D7AB5FB1DE9147E95311D94305B982B9874BE")]
    public async Task RiverAnimationNativeAssetsPreserveApprovedPixelsDurationAndNearestSampling(string asset, string hash)
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
                PrototypeDirectory = new("/RiverNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/" + asset + ".rsi");
            using (var stream = manager.ContentFileRead(path / "animation.png"))
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)), Is.EqualTo(hash), "Use the actual approved pixels.");
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
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { 110 }));
                Assert.That(Math.Max(atlas.Width, atlas.Height), Is.LessThanOrEqualTo(4096));
                Assert.That(rsi.TryGetState(BossRiverVisuals.State, out var state), Is.True);
                Assert.That(state!.DelayCount, Is.EqualTo(110));
                Assert.That(state.AnimationLength, Is.EqualTo(2.2f).Within(0.0001));
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(256, 320)));
                using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / "animation.png"));
                var sourceColumns = source.Width / rsi.Size.X;
                for (var frame = 0; frame < 110; frame++)
                for (var y = 0; y < rsi.Size.Y; y++)
                for (var x = 0; x < rsi.Size.X; x++)
                    if (atlas[frame % columns * rsi.Size.X + x, frame / columns * rsi.Size.Y + y] !=
                        source[frame % sourceColumns * rsi.Size.X + x, frame / sourceColumns * rsi.Size.Y + y])
                        Assert.Fail($"Native atlas changed {asset} frame {frame} at ({x}, {y}).");
            }
            finally
            {
                foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
                Configuration.Default.PreferContiguousImageBuffers = previous;
            }
        });
    }

    [Test]
    public void RiverAnimationPlaybackAndBoundsKeepTheApprovedSpeedPivotAndDownwardSpill()
    {
        for (var frame = 0; frame < 110; frame++)
            Assert.That(BossRiverVisuals.FrameIndex((frame + .5) * .02), Is.EqualTo(frame));
        Assert.That(BossRiverVisuals.FrameIndex(-1), Is.Zero);
        Assert.That(BossRiverVisuals.FrameIndex(double.NaN), Is.Zero);
        Assert.That(BossRiverVisuals.FrameIndex(2.2), Is.EqualTo(109));
        var bounds = BossRiverVisuals.TextureBounds(new Vector2i(256, 320));
        var pivot = new Vector2(bounds.Left, bounds.Bottom) + new Vector2(128, 320 - 128) / 16;
        Assert.That(pivot, Is.EqualTo(Vector2.Zero));
        Assert.That(bounds.Width, Is.EqualTo(16));
        Assert.That(bounds.Height, Is.EqualTo(20));
        var bottomRim = bounds.Top - 216f / 16;
        var fallingDrop = bounds.Top - 260f / 16;
        Assert.That(bottomRim, Is.EqualTo(-5.5f));
        Assert.That(fallingDrop, Is.LessThan(bottomRim));
    }
}
