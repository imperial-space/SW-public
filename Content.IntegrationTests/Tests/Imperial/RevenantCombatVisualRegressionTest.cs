using static Content.IntegrationTests.Tests.Imperial.MeleeSwingTestData;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Boss;
using Content.Client.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Weapons;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantCombatVisualRegressionTest
{
    [Test]
    public async Task NativeMosaicCellsRemainJoinedAndShareTheFloorAngleDuringCameraTurns()
    {
        var root = Environment.GetEnvironmentVariable("SPELLWARD_VISUAL_TEST_ROOT") ??
            Path.GetFullPath("../..", TestContext.CurrentContext.TestDirectory);
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/RevenantVisualNoWorld/"),
                MountOptions = new() { DirMounts = { Path.Combine(root, "Resources"), Path.Combine(root, "RobustToolbox/Resources") } }
            }
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var sprites = client.System<SpriteSystem>();
            var transforms = client.System<SharedTransformSystem>();
            var rsi = client.ResolveDependency<IResourceCache>().GetResource<RSIResource>(
                new ResPath("/Textures/Imperial/Medieval/Structures/RevenantArena/background_v28.rsi")).RSI;
            var entity = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var sprite = client.EntMan.AddComponent<SpriteComponent>(entity);
            sprite.NoRotation = false;
            sprites.SetBaseRsi((entity, sprite), rsi);
            var offsets = new[] { new Vector2(-6, -5), new Vector2(-5, -5), new Vector2(-5, -6) };
            for (var i = 0; i < offsets.Length; i++)
            {
                var layer = sprites.LayerMapReserve((entity, sprite), "cell" + i);
                sprites.LayerSetRsiState((entity, sprite), layer, "r00_c00");
                sprites.LayerSetOffset((entity, sprite), layer, offsets[i]);
            }
            foreach (var gridDegrees in new[] { 0, 37, -90 })
            for (var cameraDegrees = -360; cameraDegrees <= 360; cameraDegrees++)
            {
                var world = Angle.FromDegrees(gridDegrees);
                var eye = Angle.FromDegrees(cameraDegrees);
                var onScreen = world + eye;
                BossArenaSceneSystem.ApplyPose(sprites, (entity, sprite), onScreen);
                var direction = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, onScreen.Reduced().FlipPositive());
                var cardinal = BossArenaSceneSystem.ProfileRotation(onScreen);
                var parent = sprite.LocalMatrix * Matrix3x2.CreateRotation((float)world) * Matrix3x2.CreateRotation((float)eye);
                for (var i = 0; i < offsets.Length; i++)
                {
                    var layer = (SpriteComponent.Layer)sprite[i];
                    layer.GetLayerDrawMatrix(direction, out var local);
                    var rendered = local * parent;
                    var expectedRotation = Matrix3x2.CreateRotation((float)(onScreen - cardinal));
                    var expectedCentre = Vector2.Transform(offsets[i], expectedRotation);
                    Assert.That(Vector2.Distance(Vector2.Transform(Vector2.Zero, rendered), expectedCentre), Is.LessThan(.00002),
                        $"Cell {i}, camera {cameraDegrees}, grid {gridDegrees}: mosaic must follow the floor without gaps.");
                    Assert.That(Vector2.Distance(Vector2.TransformNormal(Vector2.UnitX, rendered),
                        Vector2.TransformNormal(Vector2.UnitX, expectedRotation)), Is.LessThan(.00002),
                        "Native direction transforms must not rotate individual cells apart.");
                }
            }
            var facing = Angle.FromDegrees(31);
            transforms.SetLocalPosition(entity, new Vector2(2, 3));
            Assert.That(MeleeSwingOverlay.TryGetSwingTransform(client.EntMan, transforms,
                entity, MapId.Nullspace, facing, out var before), Is.True);
            transforms.SetLocalPosition(entity, new Vector2(5, -1));
            Assert.That(MeleeSwingOverlay.TryGetSwingTransform(client.EntMan, transforms,
                entity, MapId.Nullspace, facing, out var after), Is.True);
            Assert.That(after.Translation - before.Translation, Is.EqualTo(new Vector2(3, -4)), "The ribbon follows the holder's movement.");
            Assert.That(Vector2.TransformNormal(Vector2.UnitX, before), Is.EqualTo(Vector2.TransformNormal(Vector2.UnitX, after)),
                "Movement does not retarget a swing already in progress.");
            Assert.That(MeleeSwingOverlay.TryGetSwingTransform(client.EntMan, transforms,
                entity, new MapId(1234), facing, out _), Is.False, "A map change must not leave a ribbon behind.");
        });
    }

    [Test]
    public void MirroredBladeProfilesKeepTheLeadingPointAndDirectionAcrossCameraAngles()
    {
        foreach (var camera in new[] { -90, 0, 37, 90, 180, 270 })
        for (var degrees = -180; degrees <= 180; degrees++)
        {
            var aim = Angle.FromDegrees(degrees).RotateVec(Vector2.UnitX);
            var (facing, rotation) = BossExecutionLineVisuals.TipPose(aim, (float)Angle.FromDegrees(camera));
            var (profile, mirror) = BossExecutionLineVisuals.TipProfile(facing);
            var model = Matrix3x2.CreateScale(mirror ? -1 : 1, 1) * Matrix3x2.CreateRotation(rotation);
            var forward = new Angle(BossExecutionLineVisuals.TipAngle(profile)).RotateVec(Vector2.UnitX);
            Assert.That(Vector2.Distance(Vector2.TransformNormal(forward, model), aim), Is.LessThan(.00001));
            var bounds = BossExecutionLineVisuals.TipBounds(profile, new Vector2i(128, 128));
            var anchor = BossExecutionLineVisuals.TipAnchorPixels(profile, new Vector2i(128, 128));
            Assert.That(Vector2.Transform(new Vector2(bounds.Left + anchor.X / 32, bounds.Top - anchor.Y / 32), model).Length(),
                Is.LessThan(.00001), "Mirroring must keep the crest on the resolved endpoint.");
        }
        Assert.That(BossExecutionLineVisuals.TipProfile(RsiDirection.West), Is.EqualTo((RsiDirection.East, true)));
        Assert.That(BossExecutionLineVisuals.TipProfile(RsiDirection.NorthWest), Is.EqualTo((RsiDirection.NorthEast, true)));
        Assert.That(BossExecutionLineVisuals.TipProfile(RsiDirection.SouthWest), Is.EqualTo((RsiDirection.SouthEast, true)));
    }

    [Test]
    public void RiverUsesOneContinuousFrameWithoutRimStripDuplication()
    {
        var root = Environment.GetEnvironmentVariable("SPELLWARD_VISUAL_TEST_ROOT") ??
            Path.GetFullPath("../..", TestContext.CurrentContext.TestDirectory);
        var source = File.ReadAllText(Path.Combine(root, "Content.Client/Imperial/Medieval/Boss/BossRiverOverlay.cs"));
        Assert.That(source, Does.Contain("handle.DrawTextureRect(frame, BossRiverVisuals.TextureBounds(frame.Size), Color.White)"));
        Assert.That(source, Does.Not.Contain("RimPatches"));
        var style = File.ReadAllText(Path.Combine(root, "Resources/Prototypes/Imperial/Medieval/Bosses/RevenantBoss/telegraphs.yml"));
        Assert.That(style, Does.Contain("warningOverflowTiles: 1"), "Only the precursor extends over the arena ledge.");
        var execution = File.ReadAllText(Path.Combine(root, "Content.Client/Imperial/Medieval/Boss/BossExecutionLineOverlay.cs"));
        Assert.That(execution, Does.Contain("line.Resolved ? length :"), "The impact must not inherit precursor overhang.");
        var bounds = BossRiverVisuals.TextureBounds(new Vector2i(256, 320));
        Assert.That(bounds.Width * 16, Is.EqualTo(256));
        Assert.That(bounds.Height * 16, Is.EqualTo(320));
        Assert.That(.7f * MeleeSwingVisuals.SwingPlaybackScale(Style, .45f), Is.EqualTo(.7f / 1.15f).Within(.00001));
        Assert.That(MeleeSwingVisuals.SwingFrame(Style, .609f, .45f), Is.EqualTo(34));
    }
}
