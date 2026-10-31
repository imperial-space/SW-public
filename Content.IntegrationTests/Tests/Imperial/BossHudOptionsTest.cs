using Content.Shared.Imperial.Medieval.Boss;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.Imperial.Medieval.Boss.HealthBar;
using Content.Client.Options.UI;
using Content.Client.Options.UI.Tabs;
using Content.Shared.CCVar;
using Content.Shared.Localizations;
using Robust.Client;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossHudOptionsTest
{
    [Test]
    public async Task AccessibilityControlsApplyAndResetBossHudPreferences()
    {
        using var client = Client();
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            var localization = client.ResolveDependency<ILocalizationManager>();
            var logger = client.ResolveDependency<ILogManager>().GetSawmill("loc");
            var level = logger.Level;
            logger.Level = LogLevel.Fatal;
            AccessibilityTab tab;
            try
            {
                var contentLocalization = new ContentLocalizationManager();
                IoCManager.InjectDependencies(contentLocalization);
                contentLocalization.Initialize();
                localization.SetCulture(CultureInfo.GetCultureInfo("en-US"));
                tab = new AccessibilityTab();
            }
            finally
            {
                logger.Level = level;
            }
            var config = client.ResolveDependency<IConfigurationManager>();
            tab.FindControl<CheckBox>("BossHudEnabledCheckBox").Pressed = false;
            tab.FindControl<OptionSlider>("BossHudScaleSlider").Slider.Value = 1.5f;
            tab.FindControl<CheckBox>("BossHudCustomPositionCheckBox").Pressed = true;
            tab.FindControl<OptionSlider>("BossHudHorizontalSlider").Slider.Value = 0.1f;
            tab.FindControl<OptionSlider>("BossHudVerticalSlider").Slider.Value = 0.9f;
            var options = ((IEnumerable<BaseOption>)typeof(OptionsTabControlRow)
                .GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tab.Control)!).ToArray();
            foreach (var option in options.Where(option => option.IsModified())) option.SaveValue();
            Assert.That(config.GetCVar(BossHudCVars.Enabled), Is.False);
            Assert.That(config.GetCVar(BossHudCVars.Scale), Is.EqualTo(1.5f).Within(0.001));
            Assert.That(config.GetCVar(BossHudCVars.CustomPosition), Is.True);
            Assert.That(config.GetCVar(BossHudCVars.PositionX), Is.EqualTo(0.1f).Within(0.001));
            Assert.That(config.GetCVar(BossHudCVars.PositionY), Is.EqualTo(0.9f).Within(0.001));
            foreach (var option in options) { option.ResetToDefault(); option.SaveValue(); }
            Assert.That(config.GetCVar(BossHudCVars.Enabled), Is.True);
            Assert.That(config.GetCVar(BossHudCVars.Scale), Is.EqualTo(1));
            Assert.That(config.GetCVar(BossHudCVars.CustomPosition), Is.False);
            tab.Dispose();
        });
    }

    [Test]
    public async Task BossHudScalesAndKeepsCustomPositionsInsideTheViewport()
    {
        using var client = Client();
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            var config = client.ResolveDependency<IConfigurationManager>();
            config.SetCVar(BossHudCVars.Scale, 1.5f);
            var parent = new LayoutContainer();
            var bar = new BossEncounterHealthBarUI();
            parent.AddChild(bar);
            var available = new Vector2(1000, 600);
            bar.SetDisplayScale(BossHudLayout.Scale(config, available.X));
            bar.SetData(0.5f, 0, "Test boss");
            bar.SetCast(true, 0.5f, "Test cast");
            parent.Measure(available);
            parent.Arrange(UIBox2.FromDimensions(Vector2.Zero, available));
            Assert.That(bar.MinWidth, Is.EqualTo(630));
            foreach (var location in new[] { Vector2.Zero, Vector2.One, new Vector2(0.2f, 0.8f) })
            {
                config.SetCVar(BossHudCVars.PositionX, location.X);
                config.SetCVar(BossHudCVars.PositionY, location.Y);
                BossHudLayout.ApplyPosition(bar, config);
                parent.Measure(available);
                parent.Arrange(UIBox2.FromDimensions(Vector2.Zero, available));
                Assert.That(bar.Position.X, Is.GreaterThanOrEqualTo(-0.1f));
                Assert.That(bar.Position.Y, Is.GreaterThanOrEqualTo(-0.1f));
                Assert.That(bar.Position.X + bar.Size.X, Is.LessThanOrEqualTo(available.X + 0.1f));
                Assert.That(bar.Position.Y + bar.Size.Y, Is.LessThanOrEqualTo(available.Y + 0.1f));
            }
            config.SetCVar(BossHudCVars.Scale, 2f);
            Assert.That(BossHudLayout.Scale(config, 300) * 420, Is.LessThanOrEqualTo(300.001f));
            Assert.That(BossHudLayout.Position(new Vector2(600, 400), new Vector2(100, 50), float.NaN, float.PositiveInfinity),
                Is.EqualTo(new Vector2(250, 297.5f)));
            parent.Dispose();
        });
    }

    private static RobustIntegrationTest.ClientIntegrationInstance Client()
        => new(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/BossHudOptionsNoWorld/"),
                MountOptions = new()
                {
                    DirMounts =
                    {
                        Path.GetFullPath("../../Resources", TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory)
                    }
                }
            },
            BeforeStart = () =>
            {
                var config = IoCManager.Resolve<IConfigurationManager>();
                if (!config.IsCVarRegistered(BossHudCVars.Enabled.Name))
                    config.GetType().GetMethod("LoadCVarsFromAssembly")!.Invoke(config, new object[] { typeof(CCVars).Assembly });
            }
        });
}
