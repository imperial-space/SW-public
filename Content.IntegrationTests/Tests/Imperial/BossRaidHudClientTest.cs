using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Imperial.Medieval.Boss;
using Content.Client.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossRaidHudClientTest
{
    [Test]
    public async Task RaidHudReceivesRosterAndCleansUpAcrossClientSystemRestarts()
    {
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/RaidHudStartupNoWorld/"),
                MountOptions = new()
                {
                    DirMounts =
                    {
                        Path.GetFullPath("../../Resources", TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory)
                    }
                }
            },
            BeforeRegisterComponents = () =>
            {
                var components = IoCManager.Resolve<IComponentFactory>();
                components.RegisterClass<HumanoidAppearanceComponent>();
                components.RegisterClass<InventorySlotsComponent>();
            },
            BeforeStart = () => IoCManager.Resolve<IEntitySystemManager>().LoadExtraSystemType<BossRaidHudSystem>()
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            client.EntMan.Startup();
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            prototypes.LoadString("""
                - type: shader
                  id: Greyscale
                  kind: source
                  path: /Textures/Shaders/greyscale.swsl
                """);
            prototypes.ResolveResults();
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            var children = ui.WindowRoot.ChildCount;
            var world = new Control { MouseFilter = Control.MouseFilterMode.Stop };
            LayoutContainer.SetAnchorPreset(world, LayoutContainer.LayoutPreset.Wide);
            ui.StateRoot.AddChild(world);
            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new BossRaidHudEvent(NetEntity.Invalid, new()));
            Assert.That(ui.WindowRoot.ChildCount, Is.EqualTo(children + 1));
            var layer = ui.WindowRoot.Children.Single(c => c.Children.Any(child => child is BossRaidHudControl));
            var hud = layer.Children.OfType<BossRaidHudControl>().Single();
            var roster = Enumerable.Range(0, 8).Select(i => new BossRaidMember(NetEntity.Invalid,
                $"Player {i + 1}", 75, 100, 50, 100, 125, 125, MobState.Alive, true)).ToList();
            hud.SetMembers(roster);
            hud.Visible = true;
            var viewport = new Vector2(1685, 707);
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Position.X, Is.InRange(1385f, 1400f), "First layout must keep the HUD in the marked right-side area.");
            Assert.That(hud.Position.Y, Is.InRange(241f, 243f));
            Assert.That(hud.Size.X, Is.LessThanOrEqualTo(192));
            Assert.That(hud.Size.Y, Is.LessThanOrEqualTo(302), "All eight occupied rows fit below the chat.");
            Assert.That(VisiblePortraits(hud), Is.EqualTo(8));
            var buttons = hud.Children.OfType<BaseButton>().ToArray();
            Assert.That(buttons.All(b => b.Size.X > 0 && b.Size.Y > 0), Is.True, "Header controls have real, clickable bounds.");
            var header = buttons[0];
            var lockButton = buttons[3];
            var hideButton = buttons[4];
            Assert.That(header.Children.OfType<Label>().Single().Text, Is.Null.Or.Empty, "The drag bar has no raid count or title.");
            var percentages = hud.Children.OfType<Label>().Where(label => label.Text == "75%").ToArray();
            Assert.That(percentages.Length, Is.EqualTo(8), "Health uses percentages, never current/max values.");
            Assert.That(hud.Children.OfType<Label>().All(label => !(label.Text?.Contains('/') ?? false)), Is.True);
            for (var i = 0; i < percentages.Length; i++)
                Assert.That(percentages[i].Position.Y + percentages[i].Size.Y,
                    Is.LessThanOrEqualTo((26 + i * 34 + 14) * (hud.SetSize.X / 200) + 0.01f),
                    $"Health text in row {i} stays above the HP bar.");
            var changedRoster = roster.ToList();
            changedRoster[0] = new(NetEntity.Invalid, "Player 1", 0, 100, 50, 100, 125, 125, MobState.Critical, true);
            hud.SetMembers(changedRoster);
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Children.OfType<Label>().Any(label => label.Text == "Player 1"), Is.True,
                "Critical status belongs on the health bar, without a CRITICAL name prefix.");
            Assert.That(hud.Children.OfType<Label>().Any(label => label.Text?.Contains("CRITICAL") ?? false), Is.False);
            var firstPortrait = hud.Children.OfType<BossRaidPortraitControl>().First();
            Assert.That(firstPortrait.IsDead, Is.False, "Critical players retain their living face colours.");
            changedRoster[0] = new(NetEntity.Invalid, "Player 1", 0, 100, 50, 100, 125, 125, MobState.Dead, true);
            hud.SetMembers(changedRoster);
            Assert.That(firstPortrait.IsDead, Is.True, "Death selects the existing greyscale shader.");
            Assert.That(firstPortrait.ModulateSelfOverride!.Value.R, Is.LessThan(1), "Dead faces are dimmed as well as desaturated.");
            hud.SetMembers(roster);
            Assert.That(firstPortrait.IsDead, Is.False, "Resurrection restores the original portrait colours.");
            Assert.That(firstPortrait.ModulateSelfOverride, Is.Null);
            Layout(ui.RootControl, viewport);
            foreach (var button in buttons)
                Assert.That(ui.MouseGetControl(Pointer(button, button.GlobalPosition + button.Size / 2)), Is.SameAs(button),
                    "The toolbar must win mouse targeting over the underlying game screen.");
            var defaultSize = hud.Size;
            Click(ui, buttons[1]);
            Assert.That(hud.SetSize.X, Is.LessThan(defaultSize.X), "The actual click invokes the resize callback.");
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Size.X, Is.LessThan(defaultSize.X), "The smaller button works through the actual UI input route.");
            Click(ui, buttons[2]);
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Size.X, Is.EqualTo(defaultSize.X).Within(0.01));
            var beforeDrag = hud.Position;
            Drag(ui, header, new Vector2(-200, 60));
            Layout(ui.RootControl, viewport);
            Assert.That(Vector2.Distance(hud.Position, beforeDrag + new Vector2(-200, 60)), Is.LessThan(0.01));
            var afterDrag = hud.Position;
            Click(ui, lockButton);
            Drag(ui, header, new Vector2(100, 40));
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Position, Is.EqualTo(afterDrag), "Lock prevents title-bar dragging.");
            Click(ui, lockButton);
            Drag(ui, header, new Vector2(-80, 20));
            Layout(ui.RootControl, viewport);
            Assert.That(Vector2.Distance(hud.Position, afterDrag + new Vector2(-80, 20)), Is.LessThan(0.01), "Unlock restores dragging.");
            hud.SetMembers(roster.Take(2).ToList());
            Layout(ui.RootControl, viewport);
            Assert.That(VisiblePortraits(hud), Is.EqualTo(2), "Unused portraits and slots are hidden.");
            Assert.That(hud.Size.Y, Is.LessThan(100), "Removing players also removes the space below them.");
            Click(ui, hideButton);
            Layout(ui.RootControl, viewport);
            Assert.That(VisiblePortraits(hud), Is.Zero);
            Assert.That(hud.Size.Y, Is.LessThan(24));
            Click(ui, hideButton);
            hud.SetMembers(roster);
            Layout(ui.RootControl, viewport);
            Assert.That(VisiblePortraits(hud), Is.EqualTo(8));
            var moved = hud.Position;

            var fitHud = new BossRaidHudControl();
            fitHud.SetMembers(roster);
            fitHud.UpdateViewport(new(1348, 566));
            Assert.That(fitHud.SetSize.X * 1.25f, Is.LessThanOrEqualTo(192.01), "The default layout also fits with 125% UI scaling.");
            Assert.That(fitHud.SetSize.Y * 1.25f, Is.LessThanOrEqualTo(302));
            Layout(ui.RootControl, viewport);
            Assert.That(hud.Position, Is.EqualTo(moved), "Roster and viewport updates keep a manually chosen position when it fits.");
            hud.SetMembers(new());
            Layout(ui.RootControl, viewport);
            Assert.That(VisiblePortraits(hud), Is.Zero);
            Assert.That(hud.Size.Y, Is.LessThan(24));
            client.EntMan.EventBus.RaiseEvent(EventSource.Network, new BossRaidHudEvent(null, new()));
            CheckStaticPortrait(client);
            Assert.That(ui.WindowRoot.ChildCount, Is.EqualTo(children + 1), "An update reuses its existing panel.");
            client.EntMan.Shutdown();
            Assert.That(ui.WindowRoot.ChildCount, Is.EqualTo(children), "Disconnect removes the raid panel from the tree.");
            client.ResolveDependency<IEntitySystemManager>().LoadExtraSystemType<BossRaidHudSystem>();
            client.EntMan.Startup();
            Assert.DoesNotThrow(() => client.EntMan.EventBus.RaiseEvent(EventSource.Network,
                new BossRaidHudEvent(NetEntity.Invalid, new())));
            client.EntMan.Shutdown();
            Assert.That(ui.WindowRoot.ChildCount, Is.EqualTo(children));
            world.Orphan();
        });
    }

    private static int VisiblePortraits(Control hud) => hud.Children.OfType<BossRaidPortraitControl>().Count(c => c.Visible);

    private static void Layout(Control layer, Vector2 viewport)
    {
        InvalidateTree(layer);
        layer.Measure(viewport);
        layer.Arrange(UIBox2.FromDimensions(Vector2.Zero, viewport));
    }

    private static void InvalidateTree(Control control)
    {
        foreach (var child in control.Children) InvalidateTree(child);
        control.InvalidateMeasure();
    }

    private static void Click(IUserInterfaceManager ui, Control control)
    {
        Key(ui, control, BoundKeyState.Down, control.GlobalPosition + control.Size / 2);
        Key(ui, control, BoundKeyState.Up, control.GlobalPosition + control.Size / 2);
    }

    private static void Drag(IUserInterfaceManager ui, Control header, Vector2 delta)
    {
        var start = header.GlobalPosition + new Vector2(4, 4);
        Key(ui, header, BoundKeyState.Down, start);
        var end = start + delta;
        Input(ui, "MouseMove", new MouseMoveEventArgs(delta * header.UIScale, Pointer(header, end)));
        Key(ui, header, BoundKeyState.Up, end);

        Input(ui, "MouseMove", new MouseMoveEventArgs(new Vector2(20, 20) * header.UIScale, Pointer(header, end + new Vector2(20, 20))));
    }

    private static ScreenCoordinates Pointer(Control control, Vector2 position) => new(position * control.UIScale, control.Root!.Window.Id);

    private static void Key(IUserInterfaceManager ui, Control control, BoundKeyState state, Vector2 pointer)
    {
        var screen = Pointer(control, pointer);
        if (state == BoundKeyState.Down)
        {
            Assert.That(ui.MouseGetControl(screen), Is.SameAs(control));
            Input(ui, "HandleCanFocusDown", screen, null);
        }
        Input(ui, state == BoundKeyState.Down ? "KeyBindDown" : "KeyBindUp",
            new BoundKeyEventArgs(EngineKeyFunctions.UIClick, state, screen, true));
        if (state == BoundKeyState.Up) Input(ui, "HandleCanFocusUp");
    }

    private static void Input(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(target, args);

    private static void CheckStaticPortrait(RobustIntegrationTest.ClientIntegrationInstance client)
    {
        var entity = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var sprite = client.EntMan.AddComponent<Robust.Client.GameObjects.SpriteComponent>(entity);
        var sprites = client.EntMan.System<Robust.Client.GameObjects.SpriteSystem>();
        var texture = client.ResolveDependency<IResourceCache>().GetResource<TextureResource>(
            "/Textures/Objects/Tools/wrench.rsi/icon.png").Texture;
        sprites.AddTextureLayer((entity, sprite), texture);
        var helper = sprites.AddTextureLayer((entity, sprite), texture);
        ((Robust.Client.GameObjects.SpriteComponent.Layer) sprite[helper]).CopyToShaderParameters = new("test-image");
        var portrait = new BossRaidPortraitControl();
        portrait.SetEntity(client.EntMan.GetNetEntity(entity));
        Input(portrait, "CaptureFace");
        var layers = (System.Collections.Generic.List<(Texture, Color, Vector2)>)
            typeof(BossRaidPortraitControl).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(portrait)!;
        Assert.That(layers.Count, Is.EqualTo(1), "Shader helper textures must never become a visible portrait, including for non-humanoid sprites.");
        var snapshot = layers[0];
        sprites.SetOffset((entity, sprite), new(0.15f, 0.15f));
        sprites.SetRotation((entity, sprite), Angle.FromDegrees(90));
        sprites.SetColor((entity, sprite), Color.Red);
        sprites.SetScale((entity, sprite), new(2, 2));
        Input(portrait, "CaptureFace");
        Assert.That(layers[0], Is.EqualTo(snapshot), "Attacks, rotation, damage flashes and sprite scale cannot alter the captured face.");

        var humanoid = client.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var headSprite = client.EntMan.AddComponent<Robust.Client.GameObjects.SpriteComponent>(humanoid);
        var inventory = client.EntMan.AddComponent<InventorySlotsComponent>(humanoid);
        inventory.VisualLayerKeys["head"] = new() { "custom-helmet", "custom-helmet-displacement" };
        AddPart(HumanoidVisualLayers.Chest, null, Color.Red);
        AddPart(HumanoidVisualLayers.Head, null, Color.White);
        AddPart(HumanoidVisualLayers.Hair, null, Color.Green);
        AddPart(HumanoidVisualLayers.Fire, null, Color.Orange);
        AddPart(null, "custom-helmet", Color.Blue);
        var displacement = AddPart(null, "custom-helmet-displacement", Color.Yellow);
        ((Robust.Client.GameObjects.SpriteComponent.Layer) headSprite[displacement]).CopyToShaderParameters = new("custom-helmet");
        AddPart(HumanoidVisualLayers.StencilMask, null, Color.Yellow);
        AddPart(null, "right-hand-weapon", Color.Red);
        portrait.SetEntity(client.EntMan.GetNetEntity(humanoid));
        Input(portrait, "CaptureFace");
        Assert.That(layers.Select(layer => layer.Item2), Is.EqualTo(new[] { Color.White, Color.Green, Color.Blue }),
            "Head, hair and helmet retain sprite order; body, combat effects, weapons and yellow shader masks are excluded.");
        var headSnapshot = layers.ToArray();
        sprites.LayerSetColor((humanoid, headSprite), HumanoidVisualLayers.Head, Color.Red);
        sprites.LayerSetScale((humanoid, headSprite), HumanoidVisualLayers.Head, new(2, 2));
        Input(portrait, "CaptureFace");
        Assert.That(layers, Is.EqualTo(headSnapshot), "Live layer changes cannot alter the captured face.");
        client.EntMan.DeleteEntity(humanoid);
        client.EntMan.DeleteEntity(entity);

        int AddPart(HumanoidVisualLayers? part, string? key, Color color)
        {
            var index = sprites.AddTextureLayer((humanoid, headSprite), texture);
            sprites.LayerSetColor((humanoid, headSprite), index, color);
            if (part != null) sprites.LayerMapSet((humanoid, headSprite), part.Value, index);
            if (key != null) sprites.LayerMapSet((humanoid, headSprite), key, index);
            return index;
        }
    }
}
