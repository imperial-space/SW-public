using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Projectiles;
using Content.Shared.Damage;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task BlackoutRemovesCenterAndRestoresItOnlyForAnOngoingFight(bool victory)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>(), Has.Length.EqualTo(1));
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new()
                { Telegraph = BossTelegraphPattern.BlackoutNorthWest, CastTime = 6, Damage = Hit(1) }), Is.True);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>(), Is.Empty);
            if (victory) server.System<BossSystem>().DamageBoss(boss, 100000);
            else server.System<BossMechanicSystem>().Cancel(boss);
            Assert.That(server.EntMan.AllEntities<BossArenaCenterBlockerComponent>(),
                victory ? Is.Empty : Has.Length.EqualTo(1));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public async Task SmallPartiesCompleteMixedSentencesWithoutConflictingAssignments(int alive)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            for (var i = alive; i < players.Length; i++) server.System<MobStateSystem>().ChangeMobState(players[i], MobState.Dead);
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.Take(alive).ToList(), boss);
            server.EntMan.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Escalation = [new() { MixedSentence = true }];
            var timing = server.ResolveDependency<IGameTiming>();
            for (var step = 0; step < 12; step++)
            {
                rotation.NextStep = timing.CurTime;
                server.System<BossEncounterSystem>().Update(0);
                var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                if (rotation.StoredTargets.TryGetValue(BossTelegraphPattern.Spread, out var spread) &&
                    rotation.StoredTargets.TryGetValue(BossTelegraphPattern.Stack, out var stack))
                    Assert.That(spread.Targets.Intersect(stack.Targets), Is.Empty);
                var duration = (mechanic.Deadline - mechanic.Start).TotalSeconds;
                Assert.That(duration, Is.GreaterThanOrEqualTo(3));
                if (!mechanic.PreloadOnly)
                {
                    var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                    Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
                }
                mechanic.Deadline = timing.CurTime;
                server.System<BossMechanicSystem>().Update(0);
                if (rotation.PendingSteps.Count == 0) break;
            }
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.Recovery, Is.EqualTo(7));
            Assert.That((rotation.NextStep - timing.CurTime).TotalSeconds, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task LegacyFightCannotTakePlayersFromRevenantArena()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var legacy = server.EntMan.SpawnEntity("SkeletonSkullBoss", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            var position = server.EntMan.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, -4.5f, 4.5f));
            server.System<TagSystem>().AddTag(position, "BossSpawnPosition");
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), legacy), Is.False);
            Assert.That(server.EntMan.GetComponent<BossComponent>(legacy).Players, Is.Empty);
            server.System<BossSystem>().StartBossfight(players.ToList(), arena.Comp.Boss);
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), legacy), Is.False);
            Assert.That(server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss).FightRunning, Is.True);
        });
    }

    [TestCase(-0.6f)]
    [TestCase(0f)]
    [TestCase(0.6f)]
    public async Task ActualFireballHitsBossThroughCenterAtDifferentOffsets(float offset)
    {
        var (server, arena, players, _) = await Setup();
        float health = 0;
        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < players.Length; i++) server.System<SharedTransformSystem>().SetCoordinates(players[i],
                new EntityCoordinates(arena.Comp.CombatGrid, i - 2, -4.5f));
            var boss = arena.Comp.Boss;
            server.EntMan.GetComponent<BossEncounterComponent>(boss).AutoStart = false;
            var fixture = server.EntMan.GetComponent<FixturesComponent>(boss).Fixtures.Values.Single();
            Assert.That(fixture.CollisionLayer & (int)CollisionGroup.BulletImpassable, Is.Not.Zero);
            health = server.EntMan.GetComponent<BossComponent>(boss).Health;
            var fireball = server.EntMan.SpawnEntity("MedievalProjectileFireballBeginner",
                new EntityCoordinates(arena.Comp.CombatGrid, -3.5f, 0.5f + offset));
            server.System<GunSystem>().ShootProjectile(fireball, new(1, 0), new(0, 0),
                players[0], players[0], speed: 8);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<BossComponent>(arena.Comp.Boss).Health,
            Is.LessThan(health), "A real fireball must hit Veyrath rather than pass through or stop at the center blocker."));
    }
}
