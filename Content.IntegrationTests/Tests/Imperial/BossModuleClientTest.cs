using System.IO;
using System.Linq;
using Robust.Client;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossModuleClientTest
{
    [Test]
    public async Task NativeSpriteAssetsLoad()
    {
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false,
            LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false,
                LoadConfigAndUserData = false,
                PrototypeDirectory = new("/BossSpriteTestNoWorld/"),
                MountOptions = new()
                {
                    DirMounts =
                    {
                        Path.GetFullPath("../../Resources", TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory),
                    }
                }
            }
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            var resources = client.ResolveDependency<IResourceCache>();
            var roots = new[]
            {
                "/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/",
                "/Textures/Imperial/Medieval/Effects/Boss/",
                "/Textures/Imperial/Medieval/Melee/revenant_scythe",
                "/Textures/Imperial/Medieval/Misc/veyrath_key.rsi/",
                "/Textures/Imperial/Medieval/Structures/BossArenaGate/",
                "/Textures/Imperial/Medieval/Structures/RevenantBoss",
                "/Textures/Imperial/Medieval/Structures/RevenantArena/",
            };
            var count = 0;
            foreach (var path in resources.ContentFindFiles(new ResPath("/Textures/Imperial/Medieval/")))
            {
                if (!roots.Any(root => path.ToString().StartsWith(root))) continue;
                if (path.ToString().EndsWith(".png") && !path.ToString().Contains(".rsi/"))
                    Assert.That(resources.GetResource<TextureResource>(path).Texture.Size.X, Is.GreaterThan(0), path.ToString());
                if (!path.ToString().EndsWith(".rsi/meta.json")) continue;
                var rsi = resources.GetResource<RSIResource>(path.Directory).RSI;
                Assert.That(rsi.Size.X % 32, Is.Zero, path.ToString());
                Assert.That(rsi.Size.Y % 32, Is.Zero, path.ToString());
                Assert.That(rsi.Count(), Is.GreaterThan(0), path.ToString());
                count++;
            }
            Assert.That(resources.GetResource<RSIResource>(new ResPath("/Textures/Mobs/Ghosts/ghost_human.rsi")).RSI.Count(), Is.GreaterThan(0));
        });
    }
}
