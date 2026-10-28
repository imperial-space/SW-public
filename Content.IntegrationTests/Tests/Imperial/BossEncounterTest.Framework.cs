using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.UnitTesting;
using Linguini.Syntax.Parser;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    private static void LoadFrameworkTestPrototypes(ServerIntegrationInstance server)
    {
        var manager = server.ResolveDependency<IPrototypeManager>();
        var changed = new Dictionary<Type, HashSet<string>>();
        manager.LoadString("""
            - type: entity
              id: FrameworkTestBoss
              parent: BaseScriptedBoss
              name: Framework test boss
              components:
              - type: BossEncounter
                openingDelay: 0
                damage:
                  cleave:
                    types: {Slash: 1}
                phases:
                - id: introduction
                  steps: [{pattern: Cross, castTime: 3}]
                - id: second
                  steps: [{pattern: Divide, castTime: 3}]
                - id: third
                  steps: [{pattern: Frons, castTime: 3}]
                - id: final
                  repeat: true
                  steps: [{pattern: Sinistra, alternate: Dextra, castTime: 3}]
            - type: entity
              id: FrameworkTestTrigger
              name: Framework test trigger
              components:
              - type: Clickable
              - type: BossSummoningTrigger
            """, changed: changed);
        manager.ReloadPrototypes(changed);
    }

    private static void ConfigureFrameworkArena(Entity<BossArenaMapComponent> arena)
    {
        arena.Comp.ArenaId = "framework-test";
        arena.Comp.BossPrototype = "FrameworkTestBoss";
        arena.Comp.SummoningPrototype = "FrameworkTestTrigger";
        arena.Comp.CenterBlockerPrototype = "BossArenaCenterBlocker";
        arena.Comp.BrazierPrototype = null;
        arena.Comp.VictoryExitPrototype = "BossArenaReturnPortal";
        arena.Comp.VictoryExitTile = new(0, 2);
        arena.Comp.HalfSize = 3;
        arena.Comp.MessagePrefix = "Boss_Encounter_";
        arena.Comp.ReplenishQuivers = false;
    }

    [Test]
    public async Task FrameworkBossUsesNeutralDefaultsAndFourPhasesAdvanceOnlyAfterCompletedImpacts()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadFrameworkTestPrototypes(server);
            ConfigureFrameworkArena(arena);
            var entities = server.EntMan;
            entities.DeleteEntity(arena.Comp.Boss);
            var boss = entities.SpawnEntity("FrameworkTestBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            arena.Comp.Boss = boss;
            for (var i = 0; i < players.Length; i++)
                server.System<SharedTransformSystem>().SetCoordinates(players[i], new EntityCoordinates(arena.Comp.CombatGrid, i - 1.5f, -1.5f));
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);
            Assert.That(entities.HasComponent<BossMusicComponent>(boss), Is.False);
            Assert.That(entities.HasComponent<BossPresentationComponent>(boss), Is.False);
            var settings = entities.GetComponent<BossTelegraphSettingsComponent>(boss);
            Assert.That(settings.CaptionPrefix, Is.EqualTo("Boss_Encounter_"));
            Assert.That(settings.TelegraphPrototype.Id, Is.EqualTo("BossEncounterTelegraph"));
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            var scheduler = server.System<BossEncounterSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            var timing = server.ResolveDependency<IGameTiming>();
            for (var stage = 0; stage < 6; stage++)
            {
                encounter.NextStep = timing.CurTime;
                scheduler.Update(0);
                Assert.That(encounter.PhaseIndex, Is.EqualTo(Math.Min(stage, 3)));
                Assert.That(encounter.Waiting, Is.True);
                var active = entities.GetComponent<BossMechanicComponent>(boss);
                var expected = stage < 3 ? new[] { BossTelegraphPattern.Cross, BossTelegraphPattern.Divide, BossTelegraphPattern.Frons }[stage]
                    : (stage % 2 == 1 ? BossTelegraphPattern.Sinistra : BossTelegraphPattern.Dextra);
                Assert.That(active.Attack.Telegraph, Is.EqualTo(expected));
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.HalfSize, Is.EqualTo(3));
                Assert.That(warning.Danger.Count + warning.Safe.Count, Is.EqualTo(49));
                Assert.That(warning.Danger.All(tile => BossTilePatterns.IsPlatform(tile, 3)), Is.True);
                if (stage == 0)
                {
                    server.System<BossSystem>().CancelBossCast(boss);
                    Assert.That(encounter.PhaseIndex, Is.Zero, "An interrupted mechanic must retry the same phase.");
                    encounter.NextStep = timing.CurTime;
                    scheduler.Update(0);
                    active = entities.GetComponent<BossMechanicComponent>(boss);
                }
                active.Deadline = timing.CurTime;
                mechanics.Update(0);
                Assert.That(encounter.Waiting, Is.False);
                Assert.That(encounter.PhaseIndex, Is.EqualTo(Math.Min(stage + 1, 3)));
            }
            Assert.That(encounter.Lap, Is.EqualTo(3));
            Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.EdgeCone, Hit(1), direction: 0), Is.True);
            Assert.That(entities.GetComponent<BossTraversalComponent>(boss).Distance, Is.EqualTo(5));
            server.System<BossTraversalSystem>().Cancel(boss);
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.BlackoutNorthWest, CastTime = 3, Damage = Hit(1) }), Is.False,
                "Blackout requires configured safe braziers.");
            settings.PreparedCastNames[BossTelegraphPattern.Cross] = "Boss_Encounter_Sentencing_Divide";
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Cross, CastTime = 3, Damage = Hit(1), CastName = mechanics.CastNameKey(boss, BossTelegraphPattern.Cross, true) }, preloadOnly: true), Is.True);
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.Preparing, Is.True);
            Assert.That(bar.CastNameKey, Is.EqualTo("Boss_Encounter_Sentencing_Divide"));
            server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(bar.Preparing, Is.False);
        });
    }

    [Test]
    public async Task FrameworkArenaSummonsConfiguredBossAndRestoresConfiguredTriggerWithOptionalAmmo()
    {
        var (server, arena, players, _) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            LoadFrameworkTestPrototypes(server);
            ConfigureFrameworkArena(arena);
            var entities = server.EntMan;
            entities.DeleteEntity(arena.Comp.Crystal!.Value);
            arena.Comp.Crystal = null;
            server.System<BossSummoningTriggerSystem>().Restore(arena);
            Assert.That(entities.GetComponent<MetaDataComponent>(arena.Comp.Crystal!.Value).EntityPrototype!.ID, Is.EqualTo("FrameworkTestTrigger"));
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            Assert.That(server.System<BossSummoningTriggerSystem>().TryActivate(arena.Comp.Crystal.Value, players[0]), Is.True);
            var quiver = entities.SpawnEntity("MedievalQuiverFullBoltIron", entities.GetComponent<TransformComponent>(players[0]).Coordinates);
            var storage = entities.GetComponent<StorageComponent>(quiver);
            var count = storage.StoredItems.Count;
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.Remove(storage.StoredItems.Keys.First(), storage.Container), Is.True);
            Assert.That(storage.StoredItems.Count, Is.EqualTo(count - 1), "Replenishment is disabled by default.");
            arena.Comp.ReplenishQuivers = true;
            var bolt = storage.StoredItems.Keys.First();
            Assert.That(containers.Remove(bolt, storage.Container), Is.True);
            Assert.That(storage.StoredItems.Count, Is.EqualTo(count - 1));
            Assert.That(entities.HasComponent<BossArenaAmmoComponent>(bolt), Is.True);
        });
        await CrystalSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<MetaDataComponent>(arena.Comp.Boss).EntityPrototype!.ID, Is.EqualTo("FrameworkTestBoss"));
            Assert.That(entities.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(players[0]));
            Assert.That(server.System<BossEncounterSystem>().Reset(arena.Comp.Boss, arena), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(arena.Comp.Crystal!.Value).EntityPrototype!.ID, Is.EqualTo("FrameworkTestTrigger"));
        });
    }

    [TestCase("en-US")]
    [TestCase("ru-RU")]
    [TestCase("nl-NL")]
    public async Task FrameworkCatalogResolvesAllNeutralMessagesAndCaptions(string culture)
    {
        var (server, _, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadRevenantLanguage(server, culture);
            var catalog = server.ResolveDependency<IResourceManager>().ContentFileReadAllText($"/Locale/{culture}/Imperial/Medieval/boss-encounter.ftl");
            Assert.That(new LinguiniParser(catalog).Parse().Errors, Is.Empty);
            var localization = server.ResolveDependency<ILocalizationManager>();
            var arguments = new (string, object)[] { ("attack", "Cross"), ("boss", "Example"), ("count", 3),
                ("interval", 2), ("minutes", "05"), ("name", "Test"), ("preload", 3), ("release", 5), ("seconds", "07") };
            foreach (Match key in Regex.Matches(catalog, @"^([A-Za-z][\w-]*)\s*=", RegexOptions.Multiline))
                Assert.That(localization.TryGetString(key.Groups[1].Value, out _, arguments), Is.True, key.Groups[1].Value);
        });
    }
}
