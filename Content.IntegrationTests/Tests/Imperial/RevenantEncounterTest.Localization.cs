using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Linguini.Syntax.Parser;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase("en-US", "Veyrath's arena gate", "Sentencing Cross", "Veyrath, the Pale Harvester")]
    [TestCase("ru-RU", "врата арены Вейрата", "Приговор: Крест", "Вейрат, Бледный Жнец")]
    [TestCase("nl-NL", "poort naar Veyraths arena", "Vonnis: Kruis", "Veyrath, de Bleke Oogster")]
    public async Task RevenantNamesMessagesAndStoredCastsResolveInTheActiveLanguage(
        string culture, string gateName, string crossName, string bossName)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var localization = server.ResolveDependency<ILocalizationManager>();
            LoadRevenantLanguage(server, culture);
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var resources = server.ResolveDependency<IResourceManager>();
            var catalog = resources.ContentFileReadAllText($"/Locale/{culture}/Imperial/Medieval/revenant-boss.ftl");
            Assert.That(catalog, Does.Not.Contain("Placeholder_Boss_Revenant_"));
            var arguments = new (string, object)[] { ("attack", "Cross"), ("boss", "RevenantBoss"), ("count", 3),
                ("interval", 2), ("minutes", "05"), ("name", "Test"), ("preload", 3), ("release", 5), ("seconds", "07") };
            foreach (Match key in Regex.Matches(catalog, @"^([A-Za-z][\w-]*)\s*=", RegexOptions.Multiline))
                Assert.That(localization.TryGetString(key.Groups[1].Value, out _, arguments), Is.True, key.Groups[1].Value);
            foreach (var id in new[] { "RevenantBoss", "RevenantBossGate", "RevenantBossGateKey",
                         "RevenantBossCoffer", "RevenantBossSummoningCrystal" })
                Assert.That(prototypes.Index<EntityPrototype>(id).CustomLocalizationID,
                    Does.StartWith("Revenant_Boss"), id);
            foreach (var prototype in prototypes.EnumeratePrototypes<EntityPrototype>().Where(proto =>
                         proto.ID.StartsWith("RevenantBoss", StringComparison.Ordinal) && proto.CustomLocalizationID != null))
            {
                Assert.That(prototype.CustomLocalizationID, Does.StartWith("Revenant_Boss"), prototype.ID);
                var key = prototype.CustomLocalizationID!;
                Assert.That(localization.GetEntityData(prototype.ID).Name, Is.EqualTo(localization.GetString(key)));
                Assert.That(localization.GetEntityData(prototype.ID).Desc, Is.EqualTo(localization.GetString(key + "_Desc")));
            }
            Assert.That(prototypes.Index<EntityPrototype>("RevenantBossGate").Name, Is.EqualTo(gateName));
            Assert.That(prototypes.Index<EntityPrototype>("RevenantBoss").Name, Is.EqualTo(bossName));
            Assert.That(localization.GetString("cmd-testboss-started", ("boss", "Example")), Does.Contain("Example"));
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var encounter = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            encounter.Repeating = encounter.Escalating = true;
            encounter.Escalation = [new() { Pattern = BossTelegraphPattern.Cross, CrossSentence = true }];
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.CastNameKey, Is.EqualTo("Revenant_Boss_Sentencing_Severance"));
            Assert.That(bar.GetLocalizedCastName(), Is.EqualTo(localization.GetString(bar.CastNameKey)));
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(bar.CastNameKey, Is.EqualTo("Revenant_Boss_Sentencing_Cross"));
            Assert.That(bar.GetLocalizedCastName(), Is.EqualTo(crossName));
            server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(bar.CastNameKey, Is.Empty);
            Assert.That(bar.GetLocalizedCastName(), Is.Empty);
        });
    }

    [Test]
    public async Task ClientCastLabelsCanChangeLanguageWithoutChangingTheServersResolvedLabel()
    {
        var (server, arena, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var localization = server.ResolveDependency<ILocalizationManager>();
            var bar = server.EntMan.GetComponent<BossCastBarComponent>(arena.Comp.Boss);
            LoadRevenantLanguage(server, "en-US");
            bar.SetCastName("Revenant_Boss_Sentencing_Severance");
            Assert.That(bar.CastName, Is.EqualTo("Sentencing Severance"));
            Assert.That(bar.CastNameKey, Is.EqualTo("Revenant_Boss_Sentencing_Severance"));
            LoadRevenantLanguage(server, "ru-RU");
            Assert.That(bar.GetLocalizedCastName(), Is.EqualTo("Приговор: Разлучение"));
            Assert.That(bar.CastName, Is.EqualTo("Sentencing Severance"));
            var timer = new BossArenaExitTimerComponent { EndTime = TimeSpan.FromSeconds(307) };
            Assert.That(timer.GetLocalizedRemainingTime(TimeSpan.Zero), Is.EqualTo("05:07"));
            bar.SetCastName("custom administrator text");
            Assert.That(bar.CastNameKey, Is.Empty);
            Assert.That(bar.GetLocalizedCastName(), Is.EqualTo("custom administrator text"));
            bar.ClearCastName();
            Assert.That(bar.GetLocalizedCastName(), Is.Empty);
        });
    }

    [Test]
    public async Task RussianEncounterCatalogCoversEveryKeyAndUsesRussianPlayerLabels()
    {
        var (server, _, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadRevenantLanguage(server, "ru-RU");
            var resources = server.ResolveDependency<IResourceManager>();
            var localization = server.ResolveDependency<ILocalizationManager>();
            var englishFiles = new[] { "boss-arena-gate", "boss-arena-rules", "boss-cast", "boss-commands",
                "boss-encounter", "boss-mechanic-commands", "boss-sequence", "boss-soundtracks", "boss-spectator",
                "boss-telegraph", "revenant-boss", "revenant-coffer", "revenant-raid" };
            var russianFiles = new[] { "boss-encounter", "boss-soundtracks", "boss-spectator", "revenant-boss", "revenant-raid" };
            var englishKeys = englishFiles.SelectMany(file => Keys(resources.ContentFileReadAllText(
                $"/Locale/en-US/Imperial/Medieval/{file}.ftl"))).ToHashSet();
            var russianKeys = new System.Collections.Generic.HashSet<string>();
            foreach (var file in russianFiles)
            {
                var catalog = resources.ContentFileReadAllText($"/Locale/ru-RU/Imperial/Medieval/{file}.ftl");
                Assert.That(new LinguiniParser(catalog).Parse().Errors, Is.Empty, file);
                foreach (var key in Keys(catalog))
                    Assert.That(russianKeys.Add(key), Is.True, "Duplicate Russian key: " + key);
            }
            Assert.That(englishKeys.Except(russianKeys), Is.Empty, "Every encounter key must have a Russian definition.");
            var arguments = new (string, object)[] { ("attack", "Крест"), ("boss", "Вейрат"), ("count", 3),
                ("id", "destination"), ("interval", 2), ("minutes", "05"), ("name", "Крест"),
                ("preload", 3), ("release", 5), ("seconds", "07") };
            foreach (var key in englishKeys)
            {
                Assert.That(localization.TryGetString(key, out var value, arguments), Is.True, key);
                if (key.StartsWith("cmd-", StringComparison.Ordinal) || key.Contains("_Command_", StringComparison.Ordinal) ||
                    key == "Boss_Soundtrack_Destination")
                    continue;
                Assert.That(Regex.IsMatch(value ?? string.Empty, "[A-Za-z]"), Is.False,
                    $"Untranslated Russian player label {key}: {value}");
            }
            Assert.That(localization.GetString("Revenant_Boss_Moving_Cross"), Is.EqualTo("Блуждающий крест"));
            Assert.That(localization.GetString("Revenant_Boss_Crystal_Rain"), Is.EqualTo("Смертоносный дождь"));
            Assert.That(localization.GetString("Revenant_Boss_Sentencing_Occidens"), Is.EqualTo("Приговор: Запад"));
            Assert.That(localization.GetString("Revenant_Boss_Transitus_Septentrio"), Is.EqualTo("Переход: Север"));
            Assert.That(localization.GetString("Revenant_Boss_Edge_Cone_Name"), Is.EqualTo("Приговор за гранью"));
            Assert.That(server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>("RevenantBossScythe").Name,
                Is.EqualTo("Конец милосердия"));
        });

        static System.Collections.Generic.IEnumerable<string> Keys(string catalog)
            => Regex.Matches(catalog, @"^([A-Za-z][\w-]*)\s*=", RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value);
    }

    private static void LoadRevenantLanguage(ServerIntegrationInstance server, string culture)
    {
        var resources = server.ResolveDependency<IResourceManager>();
        var catalog = resources.ContentFileReadAllText($"/Locale/{culture}/Imperial/Medieval/revenant-boss.ftl");
        Assert.That(new LinguiniParser(catalog).Parse().Errors, Is.Empty);
        var localization = server.ResolveDependency<ILocalizationManager>();
        var logger = server.ResolveDependency<ILogManager>().GetSawmill("loc");
        var level = logger.Level;

        logger.Level = LogLevel.Fatal;
        try
        {
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            localization.ReloadLocalizations();
        }
        finally
        {
            logger.Level = level;
        }
    }
}
