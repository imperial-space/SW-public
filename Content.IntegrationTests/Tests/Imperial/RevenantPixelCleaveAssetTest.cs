using System.IO;
using System.Linq;
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
public sealed class RevenantPixelCleaveAssetTest
{
    [Test]
    public async Task ApprovedCleaveImageLoadsItsExactPixelsWithNearestNeighbourSampling()
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
                PrototypeDirectory = new("/CleaveTextureNoWorldPrototypes/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var path = new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/cleave_boss_purple.png");
            var manager = engine.ResolveDependency<IResourceManager>();
            using (var stream = manager.ContentFileRead(path))
                Assert.That(Convert.ToHexString(SHA256.HashData(stream)),
                    Is.EqualTo("EDEC259B07348AB6243F0EC697DD47E7ECEA5A493EE7CADF7775D99BC00B93BE"),
                    "Use the approved generated image itself, rather than a geometric recreation of its palette.");
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
            Assert.That(image[588, 640].A, Is.Zero);
            Assert.That(parameters.SampleParameters.Filter, Is.False);
        });
    }

}
