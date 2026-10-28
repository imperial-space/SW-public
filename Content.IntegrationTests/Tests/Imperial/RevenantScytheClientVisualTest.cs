using static Content.IntegrationTests.Tests.Imperial.MeleeSwingTestData;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Imperial.Medieval.Weapons;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Weapons;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantScytheClientVisualTest
{
    [Test]
    public async Task NativeHeldLayersLoadAndStayAroundTheBodyThroughWieldingAndHandChanges()
    {
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/ScytheNativeVisualNoWorld/"),
                MountOptions = new()
                {
                    DirMounts =
                    {
                        Path.GetFullPath("../../Resources", TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory)
                    }
                }
            }
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var sprites = client.System<SpriteSystem>();
            var rsi = client.ResolveDependency<IResourceCache>().GetResource<RSIResource>(
                new ResPath("/Textures/Imperial/Medieval/Melee/revenant_scythe.rsi")).RSI;
            Assert.That(rsi.Size, Is.EqualTo(new Vector2i(64, 64)));
            Assert.That(rsi.Count(), Is.EqualTo(10));
            var swipe = client.ResolveDependency<IResourceCache>().GetResource<RSIResource>(
                new ResPath(Style.Texture.ToString())).RSI;
            Assert.That(swipe.Size, Is.EqualTo(new Vector2i(224, 224)));
            Assert.That(swipe.Count(), Is.EqualTo(2));
            foreach (var name in new[] { "left-to-right", "right-to-left" })
            {
                Assert.That(swipe.TryGetState(name, out var state), Is.True, name);
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir1));
                Assert.That(state.DelayCount, Is.EqualTo(35));
                Assert.That(state.AnimationLength, Is.EqualTo(.7f).Within(.0001));
                for (var frame = 0; frame < state.DelayCount; frame++)
                    Assert.That(state.GetFrame(RsiDirection.South, frame).Size, Is.EqualTo(swipe.Size));
            }
            foreach (var name in new[] { "wielded-front", "wielded-rear", "carry-front-left", "carry-rear-left", "carry-front-right", "carry-rear-right" })
            {
                Assert.That(rsi.TryGetState(name, out var state), Is.True, name);
                Assert.That(state!.RsiDirections, Is.EqualTo(RsiDirectionType.Dir4), name);
                Assert.That(state.IsAnimated, Is.False, "The review turntable is not an in-game animation.");
            }

            var user = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var sprite = client.EntMan.AddComponent<SpriteComponent>(user);
            sprites.SetBaseRsi((user, sprite), rsi);
            var body = sprites.LayerMapReserve((user, sprite), HumanoidVisualLayers.Chest);
            sprites.LayerSetRsiState((user, sprite), body, "carry-front-right");
            var front = sprites.LayerMapReserve((user, sprite), "inhand-right");
            var displacement = sprites.LayerMapReserve((user, sprite), "inhand-right-rear-displacement");
            var rear = sprites.LayerMapReserve((user, sprite), "inhand-right-rear");
            sprites.LayerSetRsiState((user, sprite), displacement, "icon");
            string[] keys = ["inhand-right", "inhand-right-rear", "inhand-right-rear-displacement"];
            var originalLayerCount = sprite.AllLayers.Count();

            foreach (var hand in new[] { HandLocation.Right, HandLocation.Left, HandLocation.Right })
            foreach (var wielded in new[] { false, true, true, false })
            {

                foreach (var key in keys)
                {
                    sprites.LayerSetVisible((user, sprite), key, false);
                    sprites.LayerSetScale((user, sprite), key, new Vector2(-.3f, .6f));
                    sprites.LayerSetRotation((user, sprite), key, Angle.FromDegrees(105));
                    sprites.LayerSetOffset((user, sprite), key, new Vector2(.3f, -.2f));
                }
                MeleeSwingVisualSystem.ApplyHeldLayers(sprites, (user, sprite), keys, hand, wielded, Style);
                front = sprites.LayerMapGet((user, sprite), "inhand-right");
                rear = sprites.LayerMapGet((user, sprite), "inhand-right-rear");
                displacement = sprites.LayerMapGet((user, sprite), "inhand-right-rear-displacement");
                body = sprites.LayerMapGet((user, sprite), HumanoidVisualLayers.Chest);
                var expectedFront = wielded ? "wielded-front" : hand == HandLocation.Left ? "carry-front-left" : "carry-front-right";
                var expectedRear = wielded ? "wielded-rear" : hand == HandLocation.Left ? "carry-rear-left" : "carry-rear-right";
                Assert.That(sprite[front].RsiState.ToString(), Is.EqualTo(expectedFront));
                Assert.That(sprite[rear].RsiState.ToString(), Is.EqualTo(expectedRear));
                Assert.That(sprite[displacement].RsiState.ToString(), Is.EqualTo("icon"), "Keep the displacement shader input state.");
                Assert.That(displacement, Is.LessThan(rear), "Shader input must precede its weapon layer.");
                Assert.That(rear, Is.LessThan(body));
                Assert.That(front, Is.GreaterThan(body));
                Assert.That(sprite.AllLayers.Count(), Is.EqualTo(originalLayerCount), "State changes must reuse existing layers.");
                foreach (var key in keys)
                {
                    var index = sprites.LayerMapGet((user, sprite), key);
                    Assert.That(sprites.TryGetLayer((user, sprite), index, out var layer, false), Is.True);
                    Assert.That(layer!.Scale, Is.EqualTo(Vector2.One));
                    Assert.That(layer.Visible, Is.True, "The held weapon must stay visible during swings.");
                    Assert.That(layer.Rotation, Is.EqualTo(Angle.Zero));
                    Assert.That(layer.Offset, Is.EqualTo(Vector2.Zero));
                }
                foreach (var cameraDegrees in new[] { -90, 0, 45, 180 })
                for (var degrees = -360; degrees <= 360; degrees++)
                {
                    var angle = Angle.FromDegrees(degrees + cameraDegrees).Reduced().FlipPositive();
                    Assert.That(sprite[front].EffectiveDirection(angle), Is.EqualTo(sprite[body].EffectiveDirection(angle)));
                    Assert.That(sprite[rear].EffectiveDirection(angle), Is.EqualTo(sprite[body].EffectiveDirection(angle)));
                }
            }
            foreach (var key in keys)
                sprites.RemoveLayer((user, sprite), key);
            Assert.That(sprite.AllLayers.Count(), Is.EqualTo(1), "Dropping the weapon removes both weapon layers and displacement.");
            Assert.That(sprites.LayerMapTryGet((user, sprite), HumanoidVisualLayers.Chest, out _, false), Is.True);

            client.ResolveDependency<IPrototypeManager>().LoadString(
                File.ReadAllText(Path.GetFullPath("../../Resources/Prototypes/Shaders/cooldown.yml", TestContext.CurrentContext.TestDirectory)));
            client.ResolveDependency<IPrototypeManager>().ResolveResults();
            var slot = new HandButton("test-left", HandLocation.Left);
            var slotSize = new Vector2(64, 64);
            slot.Measure(slotSize);
            slot.Arrange(UIBox2.FromDimensions(Vector2.Zero, slotSize));
            var item = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var itemSprite = client.EntMan.AddComponent<SpriteComponent>(item);
            sprites.SetBaseRsi((item, itemSprite), rsi);
            var itemLayer = sprites.LayerMapReserve((item, itemSprite), "item");
            sprites.LayerSetRsiState((item, itemSprite), itemLayer, "item");
            slot.SetEntity(item);
            var view = slot.Children.OfType<SpriteView>().Single(v => v.Entity?.Owner == item);

            view.Measure(slotSize);
            slot.Measure(slotSize);
            slot.Arrange(UIBox2.FromDimensions(Vector2.Zero, slotSize));
            var cachedSize = (Vector2)typeof(SpriteView).GetField("_spriteSize", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
            Assert.That(cachedSize, Is.EqualTo(new Vector2(128, 128)),
                "Populating an already measured empty slot must recalculate the 64px frame at HUD scale 2.");
            var fit = MathF.Min(view.Size.X / cachedSize.X, view.Size.Y / cachedSize.Y);
            Assert.That(view.Scale.X * fit, Is.EqualTo(1f).Within(.0001),
                "The padded item sprite must be fitted to the 64px slot rather than clipping at scale 2.");
            slot.Dispose();
        });
    }
}
