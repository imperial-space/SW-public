using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossTelegraphTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task CastPreparationFollowsTargetAndCancellationRemovesOnlyMechanicMarks()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var systems = server.EntMan;
            var bosses = server.System<BossSystem>();
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(boss, players[0]), Is.True);
            var tank = systems.GetComponent<BossFocusMarkerComponent>(boss).Marker!.Value;
            var attack = new PlayerTargetedAttack { TargetLimits = (1, 2), CastTime = 6, Telegraph = BossTelegraphPattern.Stack };
            Assert.That(bosses.TryStartBossCast(boss, attack, new[] { players[0] }, false), Is.True);
            var root = visuals.GetTelegraph(boss)!.Value;
            Assert.That(root.Comp.Danger.Count, Is.EqualTo(112));
            Assert.That(systems.AllEntities<BossActorMarkerComponent>().Count(), Is.EqualTo(2));
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            var cells = visuals.SnapshotDanger(boss);
            Assert.That(cells, Does.Not.Contain(new Robust.Shared.Maths.Vector2i(4, 4)));
            Assert.That(cells, Does.Contain(new Robust.Shared.Maths.Vector2i(0, 0)));
            Assert.That(bosses.CancelBossCast(boss), Is.True);
            Assert.That(visuals.GetTelegraph(boss), Is.Null);
            Assert.That(systems.AllEntities<BossActorMarkerComponent>().Select(ent => ent.Owner), Is.EquivalentTo(new[] { tank }));
            Assert.That(systems.GetComponent<DamageableComponent>(players[0]).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task FirstAttackerKeepsTankMarkAndDepartureHandsItToNearestLivingPlayer()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() =>
        {
            var damage = server.System<DamageableSystem>();
            var boss = arena.Comp.Boss;
            damage.TryChangeDamage(boss, Hit(), origin: players[0]);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[0]));
            damage.TryChangeDamage(boss, Hit(), origin: players[1]);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[0]));
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Critical);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(players[0]));
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Alive);
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Owner, 100, 100));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var focus = server.EntMan.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss);
            Assert.That(focus.Tank, Is.EqualTo(players[1]));
            Assert.That(server.EntMan.GetComponent<BossActorMarkerComponent>(focus.Marker!.Value).Kind, Is.EqualTo(BossActorMarkerKind.Tank));
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, -0.5f, 0.5f));
            server.System<MobStateSystem>().ChangeMobState(players[1], MobState.Dead);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(players[0])));
    }

    [Test]
    public async Task TwoPhasePreviewChangesWarningAndFinishesWithoutDamage()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() => Assert.That(server.System<BossSystem>().TryStartBossCast(arena.Comp.Boss,
            new BossTargetedAttack { CastTime = 3f, Telegraph = BossTelegraphPattern.BlueFirst }, new[] { arena.Comp.Boss }, false), Is.True));
        await RunSeconds(server, 2.5);
        await server.WaitAssertion(() =>
        {
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss)!.Value.Comp;
            Assert.That(warning.SecondPhase, Is.True);
            Assert.That(warning.ShowSecondColor, Is.True);
            Assert.That(warning.Danger.Count, Is.EqualTo(85));
        });
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss), Is.Null);
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task LosingStackTargetCancelsSafelyAndDefeatCleansAllMarks()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() =>
        {
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossCast(arena.Comp.Boss, new PlayerTargetedAttack
                { TargetLimits = (1, 2), CastTime = 5, Telegraph = BossTelegraphPattern.Stack }, new[] { players[0] }, false), Is.True);
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Owner, 100, 100));
        });
        await server.WaitRunTicks(12);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss), Is.Null);
            Assert.That(server.EntMan.HasComponent<BossCastingComponent>(arena.Comp.Boss), Is.False);
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossCast(arena.Comp.Boss, new BossTargetedAttack
                { CastTime = 5, Telegraph = BossTelegraphPattern.Cross }, new[] { arena.Comp.Boss }, false), Is.True);
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(arena.Comp.Boss, players[1]), Is.True);
            bosses.BossDefeated(arena.Comp.Boss, server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss));
            Assert.That(server.EntMan.AllEntities<BossActorMarkerComponent>(), Is.Empty);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>(), Is.Empty);
        });
    }

    [Test]
    public async Task PausingPreservesTelegraphPhaseDeadlines()
    {
        var (server, arena, players) = await Setup();
        double remaining = 0;
        await server.WaitAssertion(() => Assert.That(server.System<BossSystem>().TryStartBossCast(arena.Comp.Boss,
            new BossTargetedAttack { CastTime = 6, Telegraph = BossTelegraphPattern.Rejection }, new[] { arena.Comp.Boss }, false), Is.True));
        await RunSeconds(server, 1);
        await server.WaitAssertion(() =>
        {
            var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
            remaining = (server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss)!.Value.Comp.SwitchTime - timing.CurTime).TotalSeconds;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await RunSeconds(server, 5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss)!.Value.Comp.SecondPhase, Is.False);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var now = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime;
            Assert.That((server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss)!.Value.Comp.SwitchTime - now).TotalSeconds,
                Is.EqualTo(remaining).Within(0.05));
        });
        await RunSeconds(server, remaining + 0.2);
        await server.WaitAssertion(() => Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss)!.Value.Comp.SecondPhase, Is.True));
    }

    [Test]
    public async Task MapDeletionRemovesOwnedVisualsAndDelayedOutsideAttackerUsesNearestTank()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() =>
        {
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Owner, 100, 100));

            var effect = server.EntMan.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            server.System<SharedBossArenaRulesSystem>().TagSpell(effect, players[0]);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(), origin: effect);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(players[1]));
            Assert.That(server.System<BossSystem>().TryStartBossCast(arena.Comp.Boss,
                new BossTargetedAttack { CastTime = 5, Telegraph = BossTelegraphPattern.Frons }, new[] { arena.Comp.Boss }, false), Is.True);
            server.EntMan.DeleteEntity(arena.Owner);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(arena.Comp.Boss), Is.Null);
            Assert.That(server.EntMan.AllEntities<BossActorMarkerComponent>(), Is.Empty);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>(), Is.Empty);
        });
    }

    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid[] Players)> Setup()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false, ContentStart = true, LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (cvar, value) in PoolManager.TestCvars) options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        Entity<BossArenaMapComponent> arena = default;
        var players = new EntityUid[2];
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
            arena = new(map!.Value.Owner, server.EntMan.GetComponent<BossArenaMapComponent>(map.Value.Owner));
            server.System<SharedMapSystem>().SetPaused(map.Value.Comp.MapId, false);
            if (!server.EntMan.EntityExists(arena.Comp.Boss))
            {
                if (arena.Comp.Crystal is { } crystal) server.EntMan.DeleteEntity(crystal);
                arena.Comp.Crystal = null;
                arena.Comp.Boss = server.EntMan.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            }

            var encounter = server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss);
            encounter.AutoStart = false;
            encounter.OpeningDelay = encounter.Recovery = 3600;
            encounter.TankDamage = new DamageSpecifier();
            for (var i = 0; i < players.Length; i++)
            {
                players[i] = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, i + 0.5f, 0.5f));
                server.EntMan.RemoveComponent<ShieldOnStartupComponent>(players[i]);
                server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(players[i]);
            }
        });
        return (server, arena, players);
    }
    private static Task RunSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int)Math.Ceiling(seconds / server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().TickPeriod.TotalSeconds));

    private static DamageSpecifier Hit() => new() { DamageDict = { ["Slash"] = FixedPoint2.New(5) } };
}
