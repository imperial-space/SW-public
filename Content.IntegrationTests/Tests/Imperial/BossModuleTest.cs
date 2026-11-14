using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Chasm;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Additions;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed partial class BossModuleTest : RevenantIntegrationTestBase
{
    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid[] Players)> SetupArena()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false,
            ContentStart = true,
            LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false },
        };
        foreach (var (cvar, value) in PoolManager.TestCvars) options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        Entity<BossArenaMapComponent> arena = default;
        var players = new EntityUid[4];
        await server.WaitAssertion(() =>
        {
            (arena, players) = CreateArena(server);
        });
        return (server, arena, players);
    }

    private static (Entity<BossArenaMapComponent> Arena, EntityUid[] Players) CreateArena(ServerIntegrationInstance server, string? flow = null)
    {
        var entities = server.EntMan;
        Assert.That(server.System<MapLoaderSystem>().TryLoadMap(
            new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"), out var map, out _,
            new DeserializationOptions { InitializeMaps = true }), Is.True);
        server.System<SharedMapSystem>().SetPaused(map!.Value.Comp.MapId, false);
        Entity<BossArenaMapComponent> arena = new(map.Value.Owner, entities.GetComponent<BossArenaMapComponent>(map.Value.Owner));
        if (arena.Comp.Crystal is { } crystal) entities.DeleteEntity(crystal);
        arena.Comp.Crystal = null;
        arena.Comp.Boss = entities.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, .5f, .5f));
        if (flow != null) entities.GetComponent<BossEncounterComponent>(arena.Comp.Boss).Flow = new(flow);
        var players = new EntityUid[4];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, i + 1.5f, 1.5f));
            entities.EnsureComponent<BossArenaPartyMemberComponent>(players[i]);
            entities.RemoveComponent<ShieldOnStartupComponent>(players[i]);
        }
        Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), arena.Comp.Boss), Is.True);
        return (arena, players);
    }

    private static DamageSpecifier Hit()
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(10);
        return damage;
    }

    [Test]
    public async Task InstalledMechanicsStartAndCancelWithoutOtherFeatures()
    {
        var (server, arena, players) = await SetupArena();
        FixedPoint2 before = default;
        var nativeTank = false;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            var catalog = server.System<BossMechanicDefinitionSystem>();
            var actions = server.System<BossActionSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(boss, players[0]), Is.True);
            var definitions = catalog.For(boss).ToArray();
            TestContext.Out.WriteLine("Installed mechanic definitions: " + definitions.Length);
            foreach (var definition in definitions)
            {
                foreach (var pattern in definition.Patterns)
                {
                    Assert.That(catalog.Supports(boss, pattern), Is.True);
                    Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                    {
                        Telegraph = pattern, CastTime = 3, FollowupDelay = 2, Damage = Hit(),
                    }), Is.True, pattern.ToString());
                    Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Not.Null);
                    server.System<BossSystem>().CancelBossCast(boss);
                    Assert.That(actions.IsBusy(boss), Is.False, pattern.ToString());
                }
                foreach (var traversal in definition.Traversals)
                {
                    var step = new BossEncounterStep { Traversal = traversal };
                    Assert.That(catalog.Supports(boss, step), Is.True);
                    Assert.That(actions.Start(boss, encounter, step, Hit()), Is.True, traversal.ToString());
                    if (traversal != BossTraversalKind.CrystalRain)
                        Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Style, Is.EqualTo(definition.Style));
                    server.System<BossSystem>().CancelBossCast(boss);
                    Assert.That(actions.IsBusy(boss), Is.False);
                }
                if (definition.Slot == BossActionSlot.Execution)
                {
                    Assert.That(actions.Start(boss, encounter, new BossEncounterStep { TankBuster = true, CastTime = 3 }, Hit()), Is.True);
                    server.System<BossSystem>().CancelBossCast(boss);
                }
                if (definition.AutoProjectilePrototype != null)
                {
                    Assert.That(actions.TankAttack(boss, players[0]), Is.True);
                    actions.Cleanup(boss);
                }
                if (definition.Attack is { } attack)
                {
                    Assert.That(actions.Start(boss, attack, players), Is.True, definition.Feature);
                    server.System<BossSystem>().CancelBossCast(boss);
                }
            }
            before = entities.GetComponent<DamageableComponent>(players[0]).TotalDamage;
            nativeTank = definitions.All(definition => definition.AutoProjectilePrototype == null);
            encounter.NextTank = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromHours(1);
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossEncounterSystem>().Update(0);
            server.System<BossSystem>().CancelBossCast(boss);
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.True);
            if (nativeTank)
                Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage, Is.GreaterThan(before));
            server.System<BossSystem>().DamageBoss(boss, 100000);
            Assert.That(entities.GetComponent<BossComponent>(boss).Finished, Is.True);
            Assert.That(arena.Comp.VictoryDeadline, Is.Not.Null);
        });
    }

    [Test]
    public async Task ArenaBoundsStayTheSameWithOptionalScenery()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var grid = entities.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(arena.Comp.CombatGrid);
            var count = 0;
            for (var y = -8; y <= 8; y++)
            for (var x = -8; x <= 8; x++)
            {
                var occupied = !maps.GetTileRef(arena.Comp.CombatGrid, grid, new Vector2i(x, y)).Tile.IsEmpty;
                Assert.That(occupied, Is.EqualTo(x >= -5 && x <= 5 && y >= -5 && y <= 5), $"{x}, {y}");
                if (occupied) count++;
            }
            Assert.That(count, Is.EqualTo(121));
        });
    }
    [Test]
    public async Task PitFallUsesTheInstalledLandingRule()
    {
        var (server, arena, players) = await SetupArena();
        var returnsBody = false;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<SharedTransformSystem>().SetCoordinates(players[0],
                new EntityCoordinates(arena.Comp.CombatGrid, 7.5f, .5f));
            Assert.That(server.System<BossArenaMapSystem>().TryReturnFromPit(players[0], arena.Owner, arena.Comp), Is.True);
            returnsBody = server.System<BossFeatureSystem>().Contains("arena-entrance-return");
            entities.GetComponent<ChasmFallingComponent>(players[0]).NextDeletionTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<ChasmSystem>().Update(0);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(players[0]), Is.EqualTo(returnsBody));
            if (!returnsBody) return;
            Assert.That(server.System<MobStateSystem>().IsDead(players[0]), Is.True);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(players[0]).GridUid, Is.EqualTo(arena.Comp.CombatGrid));
        });
    }

}
