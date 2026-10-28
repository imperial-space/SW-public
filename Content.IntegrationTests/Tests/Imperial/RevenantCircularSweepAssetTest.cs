using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantCircularSweepAssetTest
{
    [TestCase("close_sweep", "292D8E3DB62539B038CEEDB6D005E1BC310B0085E51FE84D39286734DEDDFAC2", false)]
    [TestCase("donut_sweep", "22F02319EA9751376686C4782229DF594BAA3A50EEACB96D3B39E03156EFFE57", true)]
    public async Task CircularSweepLoadsTheApprovedPixelsWithNearestNeighbourSampling(string name, string hash, bool hole)
    {
        using var engine = StartAssetServer();
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/" + name + ".png");
            using (var stream = manager.ContentFileRead(path))
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)), Is.EqualTo(hash));
            var stepType = typeof(TextureResource).GetNestedType("LoadStepData", BindingFlags.NonPublic)!;
            var step = Activator.CreateInstance(stepType, nonPublic: true)!;
            stepType.GetField("Path")!.SetValue(step, path);
            typeof(TextureResource).GetMethod("LoadTextureParameters", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [manager, step]);
            typeof(TextureResource).GetMethod("LoadPreTextureData", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [manager, step]);
            using var image = (Image<Rgba32>)stepType.GetField("Image")!.GetValue(step)!;
            var parameters = (TextureLoadParameters)stepType.GetField("LoadParameters")!.GetValue(step)!;
            Assert.That(image.Size, Is.EqualTo(new Size(1280, 1280)));
            Assert.That(parameters.SampleParameters.Filter, Is.False);
            Assert.That(image[640, 638].A, hole ? Is.LessThanOrEqualTo(1) : Is.GreaterThan(200));
        });
    }

    [Test]
    public async Task CircularSweepShaderParsesAsAnUnshadedImageRevealWithAllRequiredParameters()
    {
        using var engine = StartAssetServer();
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            using var stream = manager.ContentFileRead(new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/circular_sweep.swsl"));
            using var reader = new StreamReader(stream);
            var parser = typeof(ShaderPrototype).Assembly.GetType("Robust.Client.Graphics.ShaderParser", throwOnError: true)!;
            var parsed = parser.GetMethod("Parse", [typeof(string), typeof(IResourceManager)])!
                .Invoke(null, [reader.ReadToEnd(), manager])!;
            Assert.That(parsed.GetType().GetProperty("LightMode")!.GetValue(parsed)!.ToString(), Is.EqualTo("Unshaded"));
            var uniforms = (IEnumerable)parsed.GetType().GetProperty("Uniforms")!.GetValue(parsed)!;
            var names = uniforms.Cast<object>().Select(entry => entry.GetType().GetProperty("Key")!.GetValue(entry)!.ToString()).ToArray();
            Assert.That(names, Is.EquivalentTo(new[] { "anchor_uv", "texture_size", "progress", "start_angle", "sweep_angle", "feather_angle" }));
        });
    }

    private static RobustIntegrationTest.ServerIntegrationInstance StartAssetServer()
    {
        var resources = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(PoolManager).Assembly.Location)!, "../../Resources"));
        return new(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new()
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                MountOptions = new() { DirMounts = [resources] },
                PrototypeDirectory = new("/CircularSweepNoWorldPrototypes/")
            }
        });
    }
}
