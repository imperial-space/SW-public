using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantOrbWarningRegressionTest
{
    [Test]
    public async Task OrbAndMainEyesKeepNativeMosaicCellsJoinedIndependently()
    {
        var root = Environment.GetEnvironmentVariable("SPELLWARD_VISUAL_TEST_ROOT") ??
            Path.GetFullPath("../..", TestContext.CurrentContext.TestDirectory);
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/RevenantOrbNoWorld/"),
                MountOptions = new() { DirMounts = { Path.Combine(root, "Resources"), Path.Combine(root, "RobustToolbox/Resources") } }
            },
            BeforeRegisterComponents = () => IoCManager.Resolve<IComponentFactory>().RegisterClass<BossArenaSceneComponent>(),
            BeforeStart = () => IoCManager.Resolve<IEntitySystemManager>().LoadExtraSystemType<BossArenaSceneSystem>()
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var scene = client.System<BossArenaSceneSystem>();
            var overlays = client.ResolveDependency<IOverlayManager>();
            Assert.That(overlays.AllOverlays.Count(o => o.GetType().DeclaringType == typeof(BossArenaSceneSystem) &&
                o.Space == OverlaySpace.WorldSpaceBelowEntities), Is.EqualTo(1), "Apply the viewing eye before Clyde collects sprites.");
            var sprites = client.System<SpriteSystem>();
            var entity = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            client.EntMan.AddComponent<BossArenaSceneComponent>(entity);
            var sprite = client.EntMan.AddComponent<SpriteComponent>(entity);
            sprite.NoRotation = false;
            var rsi = client.ResolveDependency<IResourceCache>().GetResource<RSIResource>(
                new ResPath("/Textures/Imperial/Medieval/Structures/RevenantArena/background_v28.rsi")).RSI;
            sprites.SetBaseRsi((entity, sprite), rsi);
            var offsets = new[] { new Vector2(-6, -5), new Vector2(-5, -5), new Vector2(-5, -6) };
            for (var i = 0; i < offsets.Length; i++)
            {
                var layer = sprites.LayerMapReserve((entity, sprite), "cell" + i);
                sprites.LayerSetRsiState((entity, sprite), layer, "r00_c00");
                sprites.LayerSetOffset((entity, sprite), layer, offsets[i]);
            }
            var main = new Eye { Position = MapCoordinates.Nullspace };
            var orb = new Eye { Position = MapCoordinates.Nullspace };
            client.ResolveDependency<IEyeManager>().CurrentEye = main;
            foreach (var mainDegrees in new[] { 0, 37, 90, 180, 270 })
            for (var orbDegrees = -180; orbDegrees <= 180; orbDegrees++)
            {
                main.Rotation = Angle.FromDegrees(mainDegrees);
                orb.Rotation = Angle.FromDegrees(orbDegrees);
                foreach (var eye in new[] { main, orb, main })
                {
                    scene.ApplyView(eye);
                    var direction = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, eye.Rotation.Reduced().FlipPositive());
                    var expected = Matrix3x2.CreateRotation((float)(eye.Rotation - BossArenaSceneSystem.ProfileRotation(eye.Rotation)));
                    for (var i = 0; i < offsets.Length; i++)
                    {
                        ((SpriteComponent.Layer)sprite[i]).GetLayerDrawMatrix(direction, out var layer);
                        var actual = layer * sprite.LocalMatrix * Matrix3x2.CreateRotation((float)eye.Rotation);
                        Assert.That(Vector2.Distance(actual.Translation, Vector2.Transform(offsets[i], expected)), Is.LessThan(.00002));
                        Assert.That(Vector2.Distance(Vector2.TransformNormal(Vector2.UnitX, actual),
                            Vector2.TransformNormal(Vector2.UnitX, expected)), Is.LessThan(.00002), "Each viewport must cancel its own native cell rotation.");
                    }
                }
            }
            var retained = sprite.LocalMatrix;
            scene.ApplyView(new Eye { Position = new MapCoordinates(Vector2.Zero, new MapId(1234)), Rotation = Angle.FromDegrees(90) });
            scene.ApplyView(null);
            Assert.That(sprite.LocalMatrix, Is.EqualTo(retained), "Other maps and empty viewports cannot re-pose this arena.");
            client.EntMan.Shutdown();
            Assert.That(overlays.AllOverlays.Any(o => o.GetType().DeclaringType == typeof(BossArenaSceneSystem)), Is.False);
        });
    }

    [Test]
    public void PrecursorsExtendBeyondTheLipWithoutChangingTheirDamageMaskOrNativeImpacts()
    {
        var style = new BossTelegraphStylePrototype
        {
            WarningOverflowTiles = 1,
            ConeRiverNorthTexture = new("north"), ConeRiverEastTexture = new("east"),
            ConeRiverSouthTexture = new("south"), ConeRiverWestTexture = new("west"),
            CrossRiverTexture = new("cross"), DivideRiverTexture = new("divide"),
            CrossRiverNorthTexture = new("crossNorth"), CrossRiverEastTexture = new("crossEast"),
            CrossRiverSouthTexture = new("crossSouth"), CrossRiverWestTexture = new("crossWest")
        };
        var cases = new[]
        {
            (BossTelegraphPattern.Cross, Vector2i.Zero, 0), (BossTelegraphPattern.Divide, Vector2i.Zero, 0),
            (BossTelegraphPattern.Cross, new Vector2i(0, 4), 0), (BossTelegraphPattern.Cross, new Vector2i(4, 0), 0),
            (BossTelegraphPattern.Cross, new Vector2i(0, -4), 0), (BossTelegraphPattern.Cross, new Vector2i(-4, 0), 0),
            (BossTelegraphPattern.CardinalCone, new Vector2i(0, 7), 0), (BossTelegraphPattern.CardinalCone, new Vector2i(7, 0), 3),
            (BossTelegraphPattern.CardinalCone, new Vector2i(0, -7), 2), (BossTelegraphPattern.CardinalCone, new Vector2i(-7, 0), 1)
        };
        var frame = BossRiverVisuals.TextureBounds(new Vector2i(256, 320));

        var leftLip = frame.Left + .5f + 40f / 16;
        var rightLip = frame.Left + .5f + 216f / 16;
        Assert.That(leftLip, Is.EqualTo(-5));
        Assert.That(rightLip, Is.EqualTo(6));
        foreach (var (pattern, origin, facing) in cases)
        {
            var damage = BossTilePatterns.Danger(pattern, [], origin: origin, facing: facing);
            var snapshot = damage.ToArray();
            var warning = BossArenaWarningVisuals.Tiles(pattern, damage, 5, style.WarningOverflowTiles, origin, facing).ToArray();
            Assert.That(warning.Where(tile => BossTilePatterns.IsPlatform(tile)), Is.EquivalentTo(snapshot));
            Assert.That(warning.Any(tile => !BossTilePatterns.IsPlatform(tile)), Is.True);
            Assert.That(warning.All(t => t.X >= leftLip - 1 && t.X + 1 <= rightLip + 1 &&
                t.Y >= leftLip - 1 && t.Y + 1 <= rightLip + 1), Is.True);
            Assert.That(damage, Is.EqualTo(snapshot));
        }
    }
}
