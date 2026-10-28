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
public sealed class RevenantExecutionSlashAssetTest
{
    [TestCase("execution_warning.rsi", "warning", 256, 96, 4, 1, 1.28f,
        "6523476F7FFD9D062C715B30CAA66733CF74B45D958FEE97553F87152C0D3A30")]
    [TestCase("execution_tip.rsi", "strike", 128, 128, 30, 8, 1.2f,
        "3365DB29BDC0B4819DC633047BB5C0FE362ACEA653F95FEFAC51697BEECBEF14")]
    [TestCase("execution_trail.rsi", "strike", 256, 96, 30, 1, 1.2f,
        "A0651976A2B79F184F48A188DA0B742A3B8AFDE168E710C8BC61ADE2CFB88D97")]
    public async Task NativeLoaderPreservesApprovedPixelsDirectionsAndTransparentWarning(
        string folder, string stateName, int width, int height, int count, int directions, float duration, string hash)
    {
        var resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(PoolManager).Assembly.Location)!, "../../Resources"));
        using var engine = new RobustIntegrationTest.ServerIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new()
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                MountOptions = new() { DirMounts = [resources] },
                PrototypeDirectory = new("/ExecutionArtNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/" + folder);
            using (var stream = manager.ContentFileRead(path / (stateName + ".png")))
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)), Is.EqualTo(hash), "Install the approved native pixels exactly.");
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
                Assert.That(rsi.Size, Is.EqualTo(new Vector2i(width, height)));
                Assert.That((int[])type.GetField("FrameCounts")!.GetValue(step)!, Is.EqualTo(new[] { count * directions }));
                Assert.That(rsi[stateName].DelayCount, Is.EqualTo(count));
                Assert.That(rsi[stateName].AnimationLength, Is.EqualTo(duration).Within(.00001));
                using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / (stateName + ".png")));
                var sourceColumns = source.Width / width;
                for (var frame = 0; frame < count * directions; frame++)
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var pixel = source[frame % sourceColumns * width + x, frame / sourceColumns * height + y];
                    if (atlas[frame % columns * width + x, frame / columns * height + y] != pixel)
                        Assert.Fail($"Native loader changed {folder} frame {frame} at {x},{y}.");
                    if (folder == "execution_warning.rsi" && y >= 16 && y < 80 && pixel.A != 0)
                        Assert.Fail("The pre-hit warning center must remain completely transparent.");
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
