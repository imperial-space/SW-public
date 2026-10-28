using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed partial class BossCastTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task CastResolvesOnceAfterDelayAndOrdinaryDamageDoesNotInterrupt()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();
        var action = new CountingAction();
        var attack = new BossTargetedAttack { CastTime = 0.5f, CastName = "Test cast", Actions = [action] };
        EntityUid boss = default;

        await server.WaitAssertion(() =>
        {
            boss = entities.SpawnEntity("RevenantBoss", CreateTestGrid(server));
            Assert.That(bosses.TryStartBossCast(boss, attack, [], false), Is.True);
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(bar.Casting, Is.True);
                Assert.That(bar.CastName, Is.EqualTo("Test cast"));
                Assert.That(bar.EndTime - bar.StartTime, Is.EqualTo(TimeSpan.FromSeconds(0.5)));
                Assert.That(action.Executions, Is.Zero, "Starting a cast must not execute its mechanic.");
                Assert.That(entities.GetComponent<BossHealthScalingComponent>(boss).Locked, Is.False,
                    "Previewing a cast must not capture the arena population.");
                Assert.That(bosses.TryStartBossCast(boss, attack, [], false), Is.False, "Casts cannot overlap.");
            });

            bosses.DamageBoss(boss, 10);
            Assert.That(bar.Casting, Is.True, "Ordinary boss damage must not interrupt the cast.");
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(action.Executions, Is.Zero));
        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1));
            Assert.That(action.Targets, Is.EqualTo(new[] { boss }));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
        });
        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1), "Completed casts must never resolve a second time.");
            entities.DeleteEntity(boss);
        });
    }

    [Test]
    public async Task CastCapturesTargetsOnceAndDropsUnavailableTargetsBeforeResolving()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        var action = new CountingAction();
        var attack = new CountingTargetAttack { CastTime = 0.5f, Actions = [action] };
        EntityUid boss = default;
        EntityUid[] targets = [];

        await server.WaitAssertion(() =>
        {
            var arena = CreateTestGrid(server);
            var otherArena = CreateTestGrid(server);
            boss = entities.SpawnEntity("RevenantBoss", arena);
            targets = Enumerable.Range(0, 4).Select(_ => entities.SpawnEntity("MobHuman", arena)).ToArray();
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss, attack, targets, false), Is.True);
            Assert.That(attack.Picks, Is.EqualTo(1));
            server.System<MobStateSystem>().ChangeMobState(targets[1], MobState.Dead);
            entities.DeleteEntity(targets[2]);
            server.System<SharedTransformSystem>().SetCoordinates(targets[3], otherArena);
        });

        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(attack.Picks, Is.EqualTo(1), "The target selection must be captured at cast start.");
            Assert.That(action.Executions, Is.EqualTo(1));
            Assert.That(action.Targets, Is.EqualTo(new[] { targets[0] }),
                "Dead, deleted and off-map targets must not receive the resolving mechanic.");
            entities.DeleteEntity(boss);
            foreach (var target in targets.Where(target => entities.EntityExists(target)))
                entities.DeleteEntity(target);
        });
    }

    [Test]
    public async Task CancelDefeatAndEncounterEndPreventPendingMechanics()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();
        var action = new CountingAction();
        var attack = new BossTargetedAttack { CastTime = 0.5f, Actions = [action] };
        EntityUid[] bossEntities = [];
        EntityUid participant = default;

        await server.WaitAssertion(() =>
        {
            var arena = CreateTestGrid(server);
            bossEntities = Enumerable.Range(0, 4).Select(_ => entities.SpawnEntity("RevenantBoss", arena)).ToArray();
            participant = entities.SpawnEntity("MobHuman", arena);
            Assert.That(bosses.TryStartBossCast(bossEntities[0], attack, [], false), Is.True);
            Assert.That(bosses.CancelBossCast(bossEntities[0]), Is.True);
            Assert.That(bosses.CancelBossCast(bossEntities[0]), Is.False);
            Assert.That(entities.GetComponent<BossCastBarComponent>(bossEntities[0]).Casting, Is.False);

            Assert.That(bosses.TryStartBossCast(bossEntities[1], attack, [], false), Is.True);
            bosses.DamageBoss(bossEntities[1], server.EntMan.GetComponent<BossComponent>(bossEntities[1]).MaxHealth * 2);
            Assert.That(entities.GetComponent<BossCastBarComponent>(bossEntities[1]).Casting, Is.False);
            Assert.That(bosses.TryStartBossCast(bossEntities[1], attack, [], false), Is.False);

            Assert.That(bosses.TryStartBossfight([participant], bossEntities[2]), Is.True);
            Assert.That(bosses.TryStartBossCast(bossEntities[2], attack, [participant]), Is.True);
            bosses.BossWon(bossEntities[2], entities.GetComponent<BossComponent>(bossEntities[2]));
            Assert.That(entities.GetComponent<BossCastBarComponent>(bossEntities[2]).Casting, Is.False);

            Assert.That(bosses.TryStartBossCast(bossEntities[3], attack, [], false), Is.True);
            entities.DeleteEntity(bossEntities[3]);
        });

        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.Zero, "Cancelled or ended encounters must not leave delayed mechanics.");
            foreach (var boss in bossEntities.Where(boss => entities.EntityExists(boss)))
                entities.DeleteEntity(boss);
            entities.DeleteEntity(participant);
        });
    }

    [Test]
    public async Task PausingPreservesRemainingCastTime()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        var action = new CountingAction();
        var attack = new BossTargetedAttack { CastTime = 0.5f, Actions = [action] };
        EntityUid boss = default;
        TimeSpan originalStart = default;
        TimeSpan originalEnd = default;
        TimeSpan pausedAt = default;

        await server.WaitAssertion(() =>
        {
            boss = entities.SpawnEntity("RevenantBoss", CreateTestGrid(server));
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss, attack, [], false), Is.True);
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            originalStart = bar.StartTime;
            originalEnd = bar.EndTime;
            pausedAt = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<MetaDataSystem>().SetEntityPaused(boss, true);
        });

        await RunSeconds(server, 0.7);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.Zero, "Paused bosses must not complete their casts.");
            var pauseDuration = server.ResolveDependency<IGameTiming>().CurTime - pausedAt;
            server.System<MetaDataSystem>().SetEntityPaused(boss, false);
            var bar = entities.GetComponent<BossCastBarComponent>(boss);
            Assert.That(bar.StartTime, Is.EqualTo(originalStart + pauseDuration));
            Assert.That(bar.EndTime, Is.EqualTo(originalEnd + pauseDuration));
            Assert.That(bar.Casting, Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(action.Executions, Is.Zero));
        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1));
            entities.DeleteEntity(boss);
        });
    }

    [Test]
    public async Task InvalidCastDurationsAreRejectedAndLegacyAttacksRemainImmediate()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();
        var action = new CountingAction();
        var attack = new BossTargetedAttack { Actions = [action] };
        EntityUid boss = default;
        EntityUid participant = default;

        await server.WaitAssertion(() =>
        {
            var arena = CreateTestGrid(server);
            boss = entities.SpawnEntity("RevenantBoss", arena);
            participant = entities.SpawnEntity("MobHuman", arena);
            Assert.That(attack.CastTime, Is.Zero);
            foreach (var duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                attack.CastTime = duration;
                Assert.That(bosses.TryStartBossCast(boss, attack, [], false), Is.False);
            }
            attack.CastTime = 0.5f;
            Assert.That(bosses.TryStartBossCast(boss, attack, []), Is.False,
                "Normal encounter casts require an active encounter.");
            attack.CastTime = 0;
            var component = entities.GetComponent<BossComponent>(boss);
            component.Stages[1].Attacks = [attack];
            component.Stages[1].StageDelay = 10;
            Assert.That(bosses.TryStartBossfight([participant], boss), Is.True);
            component.NextAttack = TimeSpan.Zero;
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1), "Existing zero-duration boss attacks must execute in their attack tick.");
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
            entities.DeleteEntity(boss);
            entities.DeleteEntity(participant);
        });
    }

    [Test]
    public async Task WipeDuringCastReturnsOnlyItsParticipantsAfterTheLinkedDelay()
    {
        var server = await StartCastServer();
        var entities = server.EntMan;
        EntityUid boss = default, otherBoss = default, participant = default, otherPlayer = default, returnPoint = default;
        var action = new CountingAction();
        await server.WaitAssertion(() =>
        {
            var arena = CreateTestGrid(server);
            boss = entities.SpawnEntity("RevenantBoss", arena);
            otherBoss = entities.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.EntityId, 2, 0));
            participant = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.EntityId, -1, 0));
            otherPlayer = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.EntityId, 1, 1));
            returnPoint = entities.SpawnEntity(null, CreateTestGrid(server));
            entities.AddComponent<BossPlayersBackPointComponent>(returnPoint).LinkId = "cast-test";
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight([participant], boss), Is.True);
            Assert.That(bosses.TryStartBossfight([otherPlayer], otherBoss), Is.True);
            var component = entities.GetComponent<BossComponent>(boss);

            component.TeleportPlayers = true;
            component.LinkId = "cast-test";
            component.SendBackDelay = TimeSpan.FromSeconds(0.5);
            Assert.That(bosses.TryStartBossCast(boss,
                new BossTargetedAttack { CastTime = 0.2f, Actions = [action] }, [participant]), Is.True);
            server.System<MobStateSystem>().ChangeMobState(participant, MobState.Dead);
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(boss), Is.True, "The boss must survive until its return timer runs.");
            Assert.That(entities.GetComponent<BossComponent>(boss).ReturnPlayersTime, Is.Not.Null);
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
            Assert.That(entities.HasComponent<BossEncounterParticipantComponent>(participant), Is.True);
            Assert.That(action.Executions, Is.Zero);
        });
        await RunSeconds(server, 0.6);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(boss), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(participant).MapID,
                Is.EqualTo(entities.GetComponent<TransformComponent>(returnPoint).MapID));
            Assert.That(entities.HasComponent<BossEncounterParticipantComponent>(participant), Is.False);
            Assert.That(entities.GetComponent<BossEncounterParticipantComponent>(otherPlayer).Boss, Is.EqualTo(entities.GetNetEntity(otherBoss)));
            Assert.That(entities.GetComponent<BossComponent>(otherBoss).Active, Is.True);
            Assert.That(action.Executions, Is.Zero);
        });
    }

    [Test]
    public async Task EncounterSchedulerHonorsCastTimeAndCooldown()
    {
        var server = await StartCastServer();
        var action = new CountingAction();
        EntityUid boss = default;
        await server.WaitAssertion(() =>
        {
            var arena = CreateTestGrid(server);
            boss = server.EntMan.SpawnEntity("RevenantBoss", arena);
            var player = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.EntityId, 2, 0));
            var component = server.EntMan.GetComponent<BossComponent>(boss);
            component.Stages[1].Attacks = [new BossTargetedAttack { CastTime = 0.5f, Cooldown = 10, Actions = [action] }];
            component.Stages[1].StageDelay = 0.1f;
            Assert.That(server.System<BossSystem>().TryStartBossfight([player], boss), Is.True);
            component.NextAttack = TimeSpan.Zero;
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Casting, Is.True);
            Assert.That(action.Executions, Is.Zero);
        });
        await RunSeconds(server, 0.7);
        await server.WaitAssertion(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1));
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
        });
    }

    private async Task<ServerIntegrationInstance> StartCastServer()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false,
            ContentStart = true,
            LoadTestAssembly = false,
            ContentAssemblies =
            [
                typeof(Content.Server.Entry.EntryPoint).Assembly,
                typeof(Content.Shared.Entry.EntryPoint).Assembly
            ],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;

        var server = StartServer(options);
        await server.WaitIdleAsync();
        return server;
    }

    private static Task RunSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int) Math.Ceiling(server.ResolveDependency<IGameTiming>().TickRate * seconds) + 1);

    private static EntityCoordinates CreateTestGrid(ServerIntegrationInstance server)
    {
        var maps = server.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: true);
        var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 2; y++)
            maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
        return new EntityCoordinates(grid.Owner, 0, 0);
    }

    [Reflect(false)]
    public sealed partial class CountingAction : BossAttackAction
    {
        public int Executions;
        public EntityUid[] Targets = [];

        public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
        {
            Executions++;
            Targets = targets.ToArray();
        }
    }

    [Reflect(false)]
    public sealed partial class CountingTargetAttack : BossAttack
    {
        public int Picks;

        public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
        {
            Picks++;
            return targets;
        }
    }
}
