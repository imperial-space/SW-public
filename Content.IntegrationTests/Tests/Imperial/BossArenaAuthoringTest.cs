using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Content.Server.Database;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.UnitTesting;
using Linguini.Syntax.Parser;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossArenaAuthoringTest : RevenantIntegrationTestBase
{
    [TestCase("en-US")]
    [TestCase("ru-RU")]
    [TestCase("nl-NL")]
    public async Task ArenaEntityNamesResolveInEachSupportedLanguage(string culture)
    {
        var (server, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var english = resources.ContentFileReadAllText("/Locale/en-US/Imperial/Medieval/boss-arena-authoring.ftl");
            var catalog = resources.ContentFileReadAllText($"/Locale/{culture}/Imperial/Medieval/boss-arena-authoring.ftl");
            Assert.That(new LinguiniParser(catalog).Parse().Errors, Is.Empty);
            var keys = new Regex(@"^([A-Za-z][A-Za-z0-9_-]*)\s*=", RegexOptions.Multiline);
            Assert.That(keys.Matches(catalog).Select(match => match.Groups[1].Value),
                Is.EquivalentTo(keys.Matches(english).Select(match => match.Groups[1].Value)));
            var localization = server.ResolveDependency<ILocalizationManager>();
            var log = server.ResolveDependency<ILogManager>().GetSawmill("loc");
            var level = log.Level;
            log.Level = LogLevel.Fatal;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                localization.ReloadLocalizations();
            }
            finally
            {
                log.Level = level;
            }
            foreach (Match key in keys.Matches(english))
                Assert.That(localization.TryGetString(key.Groups[1].Value, out var text) &&
                    !string.IsNullOrWhiteSpace(text), Is.True, key.Groups[1].Value);
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            foreach (var definition in prototypes.EnumeratePrototypes<BossArenaPrototype>())
                Assert.That(server.System<BossArenaGenerationSystem>().IsValid(definition), Is.True, definition.ID);
        });
    }

    [Test]
    public async Task GateCreatesConfiguredArenaThenSummonsFightsAndOpensItsOwnExit()
    {
        var (server, player, gate) = await Setup();
        EntityUid key = default;
        Entity<BossArenaMapComponent> arena = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            key = entities.SpawnEntity("BossArenaGateKey", entities.GetComponent<TransformComponent>(player).Coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(player, key), Is.True);
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, player, key), Is.True);
        });
        await Seconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var portal = entities.GetComponent<BossArenaGateComponent>(gate);
            Assert.That(portal.Open, Is.True);
            Assert.That(entities.EntityExists(key), Is.False);
            arena = GetArena(server, gate);
            Assert.That(arena.Comp.HalfSize, Is.EqualTo(4));
            Assert.That(arena.Comp.EntryGate, Is.EqualTo(gate));
            Assert.That(arena.Comp.ArenaPrototype!.Value.Id, Is.EqualTo("ArenaTestDefinition"));
            Assert.That(server.System<BossArenaGateSystem>().TryEnter(gate, player), Is.True);
        });
        await Seconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(player).MapUid, Is.EqualTo(arena.Owner));
            server.System<SharedTransformSystem>().SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            Assert.That(server.System<BossSummoningTriggerSystem>().TryActivate(arena.Comp.Crystal!.Value, player), Is.True);
        });
        await Seconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            Assert.That(entities.GetComponent<MetaDataComponent>(boss).EntityPrototype!.ID, Is.EqualTo("ArenaTestBoss"));
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.True);
            Assert.That(entities.HasComponent<BossMusicComponent>(boss), Is.False);
            Assert.That(entities.GetComponent<BossTelegraphSettingsComponent>(boss).Style.Id, Is.EqualTo("DefaultBossTelegraph"));
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.Danger.Count + warning.Safe.Count, Is.EqualTo(81));
            Assert.That(warning.Danger.All(tile => BossTilePatterns.IsPlatform(tile, 4)), Is.True);
            server.System<BossSystem>().DamageBoss(boss, 100000);
            Assert.That(entities.GetComponent<BossComponent>(boss).Finished, Is.True);
            Assert.That(arena.Comp.VictoryExit, Is.Not.Null);
            var exit = entities.GetComponent<BossArenaGateComponent>(arena.Comp.VictoryExit!.Value);
            Assert.That(exit.ReturnToGate, Is.True);
            Assert.That(exit.Destination, Is.EqualTo(gate));
            Assert.That(exit.Open, Is.True);
        });
    }

    [Test]
    public async Task RepeatedGatesOwnSeparateInstancesAndAnotherDefinitionUsesItsOwnSizeAndFloor()
    {
        var (server, _, first) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadPrototypes(server, """
                - type: bossArena
                  id: AuthoringLargeArena
                  bossPrototype: ArenaTestBoss
                  halfSize: 6
                  floor: FloorSteelCheckerDark
                  maxPlayers: 2
                  summoningPrototype: null
                """);
            var entities = server.EntMan;
            var coordinates = entities.GetComponent<TransformComponent>(first).Coordinates;
            var second = entities.SpawnEntity("ArenaTestGate", coordinates);
            var third = entities.SpawnEntity("ArenaTestGate", coordinates);
            entities.GetComponent<BossArenaGateComponent>(third).ArenaPrototype = "AuthoringLargeArena";
            var system = server.System<BossArenaMapSystem>();
            foreach (var gate in new[] { first, second, third })
                Assert.That(system.TryEnsureArena(gate, entities.GetComponent<BossArenaGateComponent>(gate)), Is.True);
            var a = GetArena(server, first);
            var b = GetArena(server, second);
            var c = GetArena(server, third);
            Assert.That(new[] { a.Owner, b.Owner, c.Owner }.Distinct().Count(), Is.EqualTo(3));
            Assert.That(a.Comp.Crystal, Is.Not.EqualTo(b.Comp.Crystal));
            Assert.That(c.Comp.HalfSize, Is.EqualTo(6));
            Assert.That(c.Comp.MaxPlayers, Is.EqualTo(2));
            Assert.That(c.Comp.Crystal, Is.Null);
            Assert.That(entities.EntityExists(c.Comp.Boss), Is.True);
            var maps = server.System<SharedMapSystem>();
            var grid = entities.GetComponent<MapGridComponent>(c.Comp.CombatGrid);
            ProtoId<ContentTileDefinition> floorId = "FloorSteelCheckerDark";
            var floor = server.ResolveDependency<IPrototypeManager>().Index(floorId);
            foreach (var tile in BossTilePatterns.GetPlatform(6))
                Assert.That(maps.GetTileRef(c.Comp.CombatGrid, grid, tile).Tile.TypeId, Is.EqualTo(floor.TileId));
            Assert.That(maps.GetTileRef(c.Comp.CombatGrid, grid, new Vector2i(7, 0)).Tile.IsEmpty, Is.True);
            var firstMap = a.Owner;
            Assert.That(system.TryEnsureArena(first, entities.GetComponent<BossArenaGateComponent>(first)), Is.True);
            Assert.That(GetArena(server, first).Owner, Is.EqualTo(firstMap));
            var otherCrystal = b.Comp.Crystal;
            entities.DeleteEntity(a.Comp.Crystal!.Value);
            a.Comp.Crystal = null;
            server.System<BossSummoningTriggerSystem>().Restore(a);
            Assert.That(b.Comp.Crystal, Is.EqualTo(otherCrystal));
            Assert.That(entities.EntityExists(otherCrystal!.Value), Is.True);
        });
    }

    [Test]
    public async Task InvalidDefinitionsDoNotCreateMaps()
    {
        var (server, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            (string Field, string Value)[] invalid =
            [
                ("halfSize", "1"), ("floor", "Space"), ("maxPlayers", "9"),
                ("entranceTile", "-2147483648, 0"), ("victoryExitTile", "99, 0"),
                ("victoryLootDuration", "0"), ("messagePrefix", "\"\""),
                ("entrancePrototype", "BossArenaGateKey"), ("summoningPrototype", "BossArenaGateKey"),
                ("centerBlockerPrototype", "BossArenaGateKey"), ("victoryExitPrototype", "BossArenaGateKey"),
                ("bossPrototype", "BossArenaGateKey")
            ];
            var definitions = invalid.Select((setting, index) =>
                $"- type: bossArena\n  id: InvalidArena{index}\n  bossPrototype: " +
                (setting.Field == "bossPrototype" ? setting.Value + "\n" :
                    $"ArenaTestBoss\n  {setting.Field}: {setting.Value}\n"));
            LoadPrototypes(server, string.Join("\n", definitions) + """

                - type: entity
                  id: InvalidHealthBoss
                  parent: ArenaTestBoss
                  components:
                  - type: Boss
                    health: 0
                - type: entity
                  id: InvalidPhaseBoss
                  parent: ArenaTestBoss
                  components:
                  - type: BossEncounter
                    phases:
                    - id: first
                      repeat: true
                      steps: [{pattern: Cross}]
                    - id: last
                      steps: [{pattern: Cross}]
                - type: entity
                  id: InvalidDamageBoss
                  parent: ArenaTestBoss
                  components:
                  - type: BossEncounter
                    phases:
                    - id: main
                      steps: [{pattern: Cross, damage: missing}]
                - type: entity
                  id: InvalidComboBoss
                  parent: ArenaTestBoss
                  components:
                  - type: BossEncounter
                    phases:
                    - id: main
                      steps: [{pattern: Cross, combo: true}]
                - type: entity
                  id: InvalidBlackoutBoss
                  parent: ArenaTestBoss
                  components:
                  - type: BossEncounter
                    phases:
                    - id: main
                      steps: [{pattern: BlackoutNorthWest}]
                - type: entity
                  id: OutsideArenaEntrance
                  parent: BossArenaEntrance
                  components:
                  - type: BossArenaEntrance
                    arrivalOffset: 0, 100
                - type: bossArena
                  id: OutsideArrivalArena
                  bossPrototype: ArenaTestBoss
                  entrancePrototype: OutsideArenaEntrance
                """ + string.Join("\n", new[] { "Health", "Phase", "Damage", "Combo", "Blackout" }
                    .Select(kind => $"\n- type: bossArena\n  id: Invalid{kind}Arena\n  bossPrototype: Invalid{kind}Boss\n")));
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var generator = server.System<BossArenaGenerationSystem>();
            var count = server.EntMan.AllEntities<MapComponent>().Count();
            foreach (var definition in prototypes.EnumeratePrototypes<BossArenaPrototype>()
                         .Where(definition => definition.ID != "ArenaTestDefinition"))
            {
                Assert.That(generator.IsValid(definition), Is.False, definition.ID);
                Assert.That(generator.TryCreate(definition.ID, out _), Is.False, definition.ID);
            }
            Assert.That(server.EntMan.AllEntities<MapComponent>().Count(), Is.EqualTo(count));
        });
    }

    [Test]
    public async Task ArenaValidationRejectsUnusableTimingAndDamageBeforeCreatingMaps()
    {
        var (server, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            ProtoId<BossArenaPrototype> id = "ArenaTestDefinition";
            var definition = prototypes.Index(id);
            Assert.That(prototypes.Index(definition.BossPrototype).TryGetComponent<BossEncounterComponent>(
                out var encounter, server.EntMan.ComponentFactory), Is.True);
            var settings = encounter!;
            var step = settings.Phases[0].Steps[0];
            var generator = server.System<BossArenaGenerationSystem>();
            var mapCount = server.EntMan.AllEntities<MapComponent>().Count();
            Assert.That(generator.IsValid(definition), Is.True);

            void Reject()
            {
                Assert.That(generator.IsValid(definition), Is.False);
                Assert.That(generator.TryCreate(id, out _), Is.False);
                Assert.That(server.EntMan.AllEntities<MapComponent>().Count(), Is.EqualTo(mapCount));
            }

            (Action<float> Set, float Original)[] timers =
            [
                (value => settings.OpeningDelay = value, settings.OpeningDelay),
                (value => settings.EntryGrace = value, settings.EntryGrace),
                (value => settings.IdleReset = value, settings.IdleReset),
                (value => settings.Recovery = value, settings.Recovery),
                (value => settings.TankInterval = value, settings.TankInterval),
                (value => step.CastTime = value, step.CastTime),
                (value => step.FollowupDelay = value, step.FollowupDelay)
            ];
            foreach (var timer in timers)
            {
                try
                {
                    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, -1 })
                    {
                        timer.Set(value);
                        Reject();
                    }
                }
                finally
                {
                    timer.Set(timer.Original);
                }
                Assert.That(generator.IsValid(definition), Is.True);
            }
            foreach (var timer in timers.Skip(4))
            {
                timer.Set(0);
                Reject();
                timer.Set(timer.Original);
            }
            foreach (var timer in timers.Skip(5))
            {
                timer.Set(121);
                Reject();
                timer.Set(timer.Original);
            }
            step.CastTime = 120;
            step.PreloadOnly = true;
            Reject();
            step.PreloadOnly = false;
            step.CastTime = timers[5].Original;
            var originalRecovery = step.RecoveryAfter;
            foreach (var recovery in new[] { float.NaN, float.PositiveInfinity, float.MaxValue, -1 })
            {
                step.RecoveryAfter = recovery;
                Reject();
            }
            step.RecoveryAfter = originalRecovery;
            var originalDamage = settings.Damage["cleave"];
            var zeroDamage = new DamageSpecifier();
            zeroDamage.DamageDict["Blunt"] = 0;
            var healingAttack = new DamageSpecifier();
            healingAttack.DamageDict["Blunt"] = 10;
            healingAttack.DamageDict["Slash"] = -1;
            foreach (var damage in new[] { new DamageSpecifier(), zeroDamage, healingAttack })
            {
                settings.Damage["cleave"] = damage;
                Reject();
                settings.Damage["cleave"] = originalDamage;
                Assert.That(generator.IsValid(definition), Is.True);
            }
            Assert.That(generator.IsValid(definition), Is.True);
        });
    }

    [Test]
    public async Task GeneratedArenaUsesExistingFallReturnAndRejectsAmbiguousGateSources()
    {
        var (server, player, gate) = await Setup();
        Entity<BossArenaMapComponent> arena = default;
        await server.WaitAssertion(() =>
        {
            var portal = server.EntMan.GetComponent<BossArenaGateComponent>(gate);
            portal.ArenaMapPath = new("/Maps/Imperial/Medieval/revenant-arena.yml");
            Assert.That(server.System<BossArenaMapSystem>().TryEnsureArena(gate, portal), Is.False);
            Assert.That(portal.Destination, Is.Null);
            portal.ArenaMapPath = null;
            Assert.That(server.System<BossArenaMapSystem>().TryEnsureArena(gate, portal), Is.True);
            arena = GetArena(server, gate);
            server.System<SharedTransformSystem>().SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, 6, 0));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(player, arena.Owner, arena.Comp), Is.True);
        });
        await Seconds(server, 2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<MobStateComponent>(player).CurrentState, Is.EqualTo(MobState.Dead));
            Assert.That(entities.GetComponent<TransformComponent>(player).MapUid, Is.EqualTo(arena.Owner));
            Assert.That(entities.GetComponent<TransformComponent>(player).Coordinates.Position,
                Is.EqualTo(entities.GetComponent<TransformComponent>(arena.Comp.Entrance).Coordinates.Position));
        });
    }

    [Test]
    public async Task InvalidArenaKeepsItsKeyAndLeavesTheGateClosed()
    {
        var (server, player, gate) = await Setup();
        EntityUid key = default;
        var mapsBefore = 0;
        await server.WaitAssertion(() =>
        {
            LoadPrototypes(server, """
                - type: bossArena
                  id: AuthoringInvalidUnlock
                  bossPrototype: ArenaTestBoss
                  halfSize: 0
                """);
            var entities = server.EntMan;
            entities.GetComponent<BossArenaGateComponent>(gate).ArenaPrototype = "AuthoringInvalidUnlock";
            key = entities.SpawnEntity("BossArenaGateKey", entities.GetComponent<TransformComponent>(player).Coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(player, key), Is.True);
            mapsBefore = entities.AllEntities<MapComponent>().Count();
            Assert.That(server.System<BossArenaGateSystem>().TryUnlock(gate, player, key), Is.True);
        });
        await Seconds(server, 3.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var portal = entities.GetComponent<BossArenaGateComponent>(gate);
            Assert.That(portal.Open, Is.False);
            Assert.That(portal.Destination, Is.Null);
            Assert.That(entities.EntityExists(key), Is.True);
            Assert.That(server.System<SharedHandsSystem>().GetActiveItem(player), Is.EqualTo(key));
            Assert.That(entities.AllEntities<MapComponent>().Count(), Is.EqualTo(mapsBefore));
        });
    }

    [Test]
    public async Task TraversalCanBeCancelledRepeatedlyAndBossCanBeDeletedMidAttack()
    {
        var (server, player, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(server.System<BossArenaMapSystem>().TryEnsureArena(gate,
                entities.GetComponent<BossArenaGateComponent>(gate)), Is.True);
            var arena = GetArena(server, gate);
            entities.DeleteEntity(arena.Comp.Crystal!.Value);
            arena.Comp.Crystal = null;
            arena.Comp.SummoningPrototype = null;
            server.System<BossSummoningTriggerSystem>().Restore(arena);
            var boss = arena.Comp.Boss;
            server.System<SharedTransformSystem>().SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
            Assert.That(server.System<BossSystem>().TryStartBossfight(new List<EntityUid> { player }, boss), Is.True);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 1;
            var traversal = server.System<BossTraversalSystem>();
            Assert.That(traversal.TryStart(boss, BossTraversalKind.EdgeCone, damage, direction: 0), Is.True);
            Assert.That(traversal.Cancel(boss), Is.True);
            Assert.That(traversal.Cancel(boss), Is.False);
            Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
            Assert.That(traversal.TryStart(boss, BossTraversalKind.EdgeCone, damage, direction: 0), Is.True);
            entities.DeleteEntity(boss);
            Assert.That(entities.AllEntities<BossTraversalComponent>(), Is.Empty);
        });
    }

    private static Entity<BossArenaMapComponent> GetArena(ServerIntegrationInstance server, EntityUid gate)
    {
        var entrance = server.EntMan.GetComponent<BossArenaGateComponent>(gate).Destination!.Value;
        var map = server.EntMan.GetComponent<TransformComponent>(entrance).MapUid!.Value;
        return new(map, server.EntMan.GetComponent<BossArenaMapComponent>(map));
    }

    private static void LoadPrototypes(ServerIntegrationInstance server, string yaml)
    {
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var changed = new Dictionary<Type, HashSet<string>>();
        prototypes.LoadString(yaml, changed: changed);
        prototypes.ReloadPrototypes(changed);
    }

    private static Task Seconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int)Math.Ceiling(server.ResolveDependency<IGameTiming>().TickRate * seconds) + 1);

    private async Task<(ServerIntegrationInstance Server, EntityUid Player, EntityUid Gate)> Setup()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false,
            ContentStart = true,
            LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        var sessions = await server.AddDummySessions(1);
        var record = Task.CompletedTask;
        await server.WaitPost(() => record = server.ResolveDependency<IServerDbManager>()
            .UpdatePlayerRecordAsync(sessions[0].UserId, sessions[0].Name, IPAddress.Loopback, null));
        await record;
        EntityUid player = default, gate = default;
        await server.WaitAssertion(() =>
        {
            LoadPrototypes(server, """
                - type: bossArena
                  id: ArenaTestDefinition
                  bossPrototype: ArenaTestBoss
                  halfSize: 4
                - type: entity
                  id: ArenaTestBoss
                  parent: BaseScriptedBoss
                  name: arena test boss
                  components:
                  - type: Boss
                    health: 1000
                  - type: BossEncounter
                    openingDelay: 5
                    recovery: 4
                    tankInterval: 6
                    tankDamage:
                      types: {Blunt: 5}
                    damage:
                      cleave:
                        types: {Blunt: 15}
                    phases:
                    - id: introduction
                      steps:
                      - {pattern: Frons, castTime: 4}
                      - {pattern: Tergum, castTime: 4}
                    - id: main
                      repeat: true
                      steps:
                      - {pattern: Cross, alternate: Divide, castTime: 3}
                      - {pattern: Sinistra, alternate: Dextra, castTime: 3}
                - type: entity
                  id: ArenaTestGate
                  parent: BaseBossArenaGate
                  name: arena test gate
                  components:
                  - type: BossArenaGate
                    arenaPrototype: ArenaTestDefinition
                """);
            var maps = server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            player = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            gate = server.EntMan.SpawnEntity("ArenaTestGate", new EntityCoordinates(grid.Owner, 0.5f, 1.5f));
            server.PlayerMan.SetAttachedEntity(sessions[0], player);
        });
        return (server, player, gate);
    }
}
