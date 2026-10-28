using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossFrameworkTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task RevenantScalesOnFirstHitFromSoloToEightPlayers()
    {
        var server = await StartBossServer();
        var sessions = await server.AddDummySessions(10);
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var players = sessions.Select(_ => entities.SpawnEntity("MobHuman", coordinates)).ToArray();

            var expected = new[] { 20000, 20000, 22857, 25714, 28571, 31429, 34286, 37143, 40000, 40000, 40000 };
            for (var count = 0; count <= 10; count++)
            {
                for (var i = 0; i < sessions.Length; i++)
                    server.PlayerMan.SetAttachedEntity(sessions[i], i < count ? players[i] : null);

                var boss = entities.SpawnEntity("RevenantBoss", coordinates);
                var component = entities.GetComponent<BossComponent>(boss);
                var scaling = entities.GetComponent<BossHealthScalingComponent>(boss);
                var bar = entities.GetComponent<BossHealthBarComponent>(boss);
                var bosses = server.System<BossSystem>();
                bosses.DamageBoss(boss, -50);
                Assert.That(scaling.Locked, Is.False, "Healing a fresh boss must not capture the player count.");

                var hit = count >= 3 ? 25000 : 100;
                bosses.DamageBoss(boss, hit);
                Assert.Multiple(() =>
                {
                    Assert.That(scaling.Locked, Is.True);
                    Assert.That(scaling.PlayerCount, Is.EqualTo(count));
                    Assert.That(component.MaxHealth, Is.EqualTo(expected[count]), $"Player count: {count}");
                    Assert.That(component.Health, Is.EqualTo(expected[count] - hit));
                    Assert.That(component.Finished, Is.False);
                    Assert.That(bar.MaxHp, Is.EqualTo(expected[count]));
                    Assert.That(bar.CurrentHp, Is.EqualTo(component.Health));
                });

                foreach (var session in sessions)
                    server.PlayerMan.SetAttachedEntity(session, null);
                bosses.DamageBoss(boss, -100000);
                Assert.That(component.Health, Is.EqualTo(expected[count]), "Leaving must not lower the healing cap.");
                bosses.DamageBoss(boss, 1);
                Assert.That(component.MaxHealth, Is.EqualTo(expected[count]), "Later hits must not recount the arena.");
                entities.DeleteEntity(boss);
            }

            foreach (var player in players)
                entities.DeleteEntity(player);
        });
    }

    [Test]
    public async Task BossEncounterCountsOnlyEligibleArenaPlayersAndLocksHealth()
    {
        var server = await StartBossServer();
        var sessions = await server.AddDummySessions(7);
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var otherCoordinates = CreateTestGrid(server);
            var boss = entities.SpawnEntity("RevenantBoss", coordinates);
            var otherBoss = entities.SpawnEntity("RevenantBoss", coordinates);
            var bodies = sessions.Select(_ => entities.SpawnEntity("MobHuman", coordinates)).ToArray();
            var npc = entities.SpawnEntity("MobHuman", coordinates);
            for (var i = 0; i < bodies.Length; i++)
                server.PlayerMan.SetAttachedEntity(sessions[i], bodies[i]);

            server.System<MobStateSystem>().ChangeMobState(bodies[2], MobState.Dead);
            entities.AddComponent<GhostComponent>(bodies[3]);
            server.System<SharedTransformSystem>().SetCoordinates(bodies[4], new EntityCoordinates(coordinates.EntityId, 30, 0));
            server.System<SharedTransformSystem>().SetCoordinates(bodies[5], otherCoordinates);
            entities.AddComponent<BossEncounterParticipantComponent>(bodies[6]).Boss = entities.GetNetEntity(otherBoss);

            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { bodies[0], bodies[1] }, boss), Is.True);
            var component = entities.GetComponent<BossComponent>(boss);
            var scaling = entities.GetComponent<BossHealthScalingComponent>(boss);
            Assert.That(scaling.PlayerCount, Is.EqualTo(2));
            Assert.That(component.MaxHealth, Is.EqualTo(22857));
            Assert.That(entities.GetComponent<BossHealthBarComponent>(boss).MaxHp, Is.EqualTo(22857));

            server.System<MobStateSystem>().ChangeMobState(bodies[1], MobState.Dead);
            server.System<SharedTransformSystem>().SetCoordinates(bodies[4], coordinates);
            bosses.DamageBoss(boss, 100);
            Assert.That(component.Health, Is.EqualTo(22757));
            Assert.That(component.MaxHealth, Is.EqualTo(22857), "Deaths and arrivals after activation must not rescale health.");

            entities.DeleteEntity(boss);
            entities.DeleteEntity(otherBoss);
            entities.DeleteEntity(npc);
            foreach (var session in sessions)
                server.PlayerMan.SetAttachedEntity(session, null);
            foreach (var body in bodies)
                entities.DeleteEntity(body);
        });
    }

    [Test]
    public async Task RevenantTakesMeleeAndSpellDamageBeforeEncounterStarts()
    {
        var server = await StartBossServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var boss = entities.SpawnEntity("RevenantBoss", coordinates);
            var attacker = entities.SpawnEntity("MobHuman", new EntityCoordinates(coordinates.EntityId, 1, 0));
            var spell = entities.SpawnEntity("MedievalActionLightningMiddle", coordinates);
            var component = entities.GetComponent<BossComponent>(boss);
            var damageable = entities.GetComponent<DamageableComponent>(boss);
            var bar = entities.GetComponent<BossHealthBarComponent>(boss);

            Assert.Multiple(() =>
            {
                Assert.That(component.Active, Is.False);
                Assert.That(component.Health, Is.EqualTo(20000));
                Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(bar.CurrentHp, Is.EqualTo(20000));
                Assert.That(bar.MaxHp, Is.EqualTo(20000));
                Assert.That(bar.Visible, Is.True);
            });

            server.System<SharedCombatModeSystem>().SetInCombatMode(attacker, true);
            var weapon = entities.GetComponent<MeleeWeaponComponent>(attacker);
            Assert.That(server.System<SharedMeleeWeaponSystem>()
                .AttemptLightAttack(attacker, attacker, weapon, boss), Is.True);
            var meleeDamage = damageable.TotalDamage.Float();
            Assert.That(meleeDamage, Is.GreaterThan(0), "A normal melee attack must reach the boss's Damageable component.");
            Assert.That(component.Health, Is.EqualTo(20000 - meleeDamage));
            Assert.That(bar.CurrentHp, Is.EqualTo(component.Health));

            var lightning = (MedievalLightningSpellEvent) server.System<SharedActionsSystem>().GetEvent(spell)!;
            var spellDamage = lightning.LightningCollideEffects.OfType<HealthChange>().Single();
            spellDamage.Effect(new EntityEffectBaseArgs(boss, entities));
            Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(meleeDamage + 26));
            Assert.That(component.Health, Is.EqualTo(19974 - meleeDamage));
            Assert.That(bar.CurrentHp, Is.EqualTo(component.Health));

            var heal = new DamageSpecifier();
            heal.DamageDict["Shock"] = FixedPoint2.New(-7);
            server.System<DamageableSystem>().TryChangeDamage(boss, heal);
            Assert.That(component.Health, Is.EqualTo(19981 - meleeDamage));
            Assert.That(bar.CurrentHp, Is.EqualTo(component.Health));

            bosses.DamageBoss(boss, 50);
            Assert.That(component.Health, Is.EqualTo(19931 - meleeDamage));
            bosses.DamageBoss(boss, -1000);
            Assert.Multiple(() =>
            {
                Assert.That(component.Health, Is.EqualTo(20000), "Healing cannot exceed the configured maximum health.");
                Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(bar.CurrentHp, Is.EqualTo(20000));
                Assert.That(bar.MaxHp, Is.EqualTo(20000));
                Assert.That(bar.Visible, Is.True);
                Assert.That(component.Active, Is.False, "Taking damage does not add AI or start the encounter controller.");
                Assert.That(entities.GetComponent<MobStateComponent>(boss).CurrentState, Is.EqualTo(MobState.Alive));
            });

            entities.DeleteEntity(spell);
            entities.DeleteEntity(attacker);
            entities.DeleteEntity(boss);
        });
    }

    [Test]
    public async Task InactiveRevenantDefeatsOnceAndCannotBeHealed()
    {
        var server = await StartBossServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();
        var outcomes = server.System<BossOutcomeListenerSystem>();

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var boss = entities.SpawnEntity("RevenantBoss", coordinates);
            var player = entities.SpawnEntity("MobHuman", coordinates);
            var spell = entities.SpawnEntity("MedievalActionLightningMiddle", coordinates);
            var component = entities.GetComponent<BossComponent>(boss);
            var damageable = entities.GetComponent<DamageableComponent>(boss);
            var bar = entities.GetComponent<BossHealthBarComponent>(boss);
            var lightning = (MedievalLightningSpellEvent) server.System<SharedActionsSystem>().GetEvent(spell)!;
            var spellDamage = lightning.LightningCollideEffects.OfType<HealthChange>().Single();

            outcomes.Defeated.Clear();
            var spellHits = (int)Math.Ceiling(component.MaxHealth / 26);
            for (var i = 0; i < spellHits; i++)
                spellDamage.Effect(new EntityEffectBaseArgs(boss, entities));

            Assert.Multiple(() =>
            {
                Assert.That(component.Active, Is.False);
                Assert.That(component.Finished, Is.True);
                Assert.That(component.Health, Is.Zero);
                Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(spellHits * 26));
                Assert.That(entities.GetComponent<MobStateComponent>(boss).CurrentState, Is.EqualTo(MobState.Dead));
                Assert.That(bar.CurrentHp, Is.Zero);
                Assert.That(bar.MaxHp, Is.EqualTo(20000));
                Assert.That(bar.Visible, Is.False);
                Assert.That(outcomes.Defeated, Is.EqualTo(new[] { boss }));
            });

            spellDamage.Effect(new EntityEffectBaseArgs(boss, entities));
            var heal = new DamageSpecifier();
            heal.DamageDict["Shock"] = FixedPoint2.New(-1000);
            heal.DamageDict["Heat"] = FixedPoint2.New(-1000);
            server.System<DamageableSystem>().TryChangeDamage(boss, heal);
            bosses.DamageBoss(boss, -1000);
            bosses.DamageBoss(boss, 1000);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(spellHits * 26));
                Assert.That(component.Health, Is.Zero);
                Assert.That(entities.GetComponent<MobStateComponent>(boss).CurrentState, Is.EqualTo(MobState.Dead));
                Assert.That(bar.CurrentHp, Is.Zero);
                Assert.That(bar.Visible, Is.False);
                Assert.That(outcomes.Defeated, Is.EqualTo(new[] { boss }));
                Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player }, boss), Is.False);
            });

            entities.DeleteEntity(spell);
            entities.DeleteEntity(player);
            entities.DeleteEntity(boss);
            outcomes.Defeated.Clear();
        });
    }

    [Test]
    public async Task RevenantUsesBossLifecycleWithOptInPreparedCombo()
    {
        var server = await StartBossServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();
        var outcomes = server.System<BossOutcomeListenerSystem>();

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var boss = entities.SpawnEntity("RevenantBoss", coordinates);
            var otherBoss = entities.SpawnEntity("RevenantBoss", coordinates);
            var player = entities.SpawnEntity("MobHuman", coordinates);
            var otherPlayer = entities.SpawnEntity("MobHuman", coordinates);
            var component = entities.GetComponent<BossComponent>(boss);
            var originalCoordinates = entities.GetComponent<TransformComponent>(player).Coordinates;

            Assert.Multiple(() =>
            {
                Assert.That(component.Active, Is.False);
                Assert.That(component.Health, Is.EqualTo(20000));
                Assert.That(component.TeleportPlayers, Is.False);
                Assert.That(component.Stages, Has.Count.EqualTo(1));
                Assert.That(component.Stages[1].Attacks.Single(), Is.TypeOf<BossPreparedSequenceAttack>());
                Assert.That(entities.HasComponent<HTNComponent>(boss), Is.False);
                Assert.That(component.UseDamageableHealth, Is.True);
                Assert.That(entities.HasComponent<DamageableComponent>(boss), Is.True);
            });

            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player, player }, boss), Is.True);
            Assert.That(component.Players, Has.Count.EqualTo(1));
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player }, boss), Is.False);
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player }, otherBoss), Is.False);
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { otherPlayer }, otherBoss), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(player).Coordinates, Is.EqualTo(originalCoordinates));

            component.NextAttack = TimeSpan.Zero;
            bosses.Update(0);
            Assert.That(component.Active, Is.True);
            Assert.That(component.Stage, Is.EqualTo(1));

            bosses.DamageBoss(boss, 50);
            Assert.That(component.Health, Is.EqualTo(19950));
            bosses.DamageBoss(boss, -100);
            Assert.That(component.Health, Is.EqualTo(20000));
            Assert.That(entities.GetComponent<DamageableComponent>(boss).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(component.Stage, Is.EqualTo(1));

            outcomes.Defeated.Clear();
            bosses.DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);
            bosses.DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2);

            Assert.Multiple(() =>
            {
                Assert.That(component.Active, Is.False);
                Assert.That(component.Finished, Is.True);
                Assert.That(component.Health, Is.Zero);
                Assert.That(entities.GetComponent<MobStateComponent>(boss).CurrentState, Is.EqualTo(MobState.Dead));
                Assert.That(entities.GetComponent<BossHealthBarComponent>(boss).Visible, Is.False);
                Assert.That(outcomes.Defeated, Is.EqualTo(new[] { boss }));
                Assert.That(entities.HasComponent<BossEncounterParticipantComponent>(player), Is.False);
                Assert.That(entities.GetComponent<TransformComponent>(player).Coordinates, Is.EqualTo(originalCoordinates));
                Assert.That(entities.GetComponent<BossEncounterParticipantComponent>(otherPlayer).Boss, Is.EqualTo(entities.GetNetEntity(otherBoss)));
                Assert.That(entities.GetComponent<BossComponent>(otherBoss).Active, Is.True);
            });
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player }, boss), Is.False);

            bosses.DamageBoss(otherBoss, server.EntMan.GetComponent<BossComponent>(otherBoss).MaxHealth * 2);
            entities.DeleteEntity(boss);
            entities.DeleteEntity(otherBoss);
            entities.DeleteEntity(player);
            entities.DeleteEntity(otherPlayer);
            outcomes.Defeated.Clear();
        });
    }

    [Test]
    public async Task SkullStartWithoutArenaMarkersIsRejected()
    {
        var server = await StartBossServer();
        var entities = server.EntMan;
        var bosses = server.System<BossSystem>();

        await server.WaitAssertion(() =>
        {
            var coordinates = CreateTestGrid(server);
            var boss = entities.SpawnEntity("SkeletonSkullBoss", coordinates);
            var player = entities.SpawnEntity("MobHuman", coordinates);
            var component = entities.GetComponent<BossComponent>(boss);

            Assert.That(component.TeleportPlayers, Is.True);
            Assert.That(component.UseDamageableHealth, Is.False);
            Assert.That(entities.HasComponent<DamageableComponent>(boss), Is.False);
            bosses.DamageBoss(boss, 500);
            Assert.That(component.Health, Is.EqualTo(4500), "The skull retains its original scripted-damage behavior.");
            Assert.That(entities.GetComponent<BossHealthBarComponent>(boss).Active, Is.False);
            Assert.That(bosses.TryStartBossfight(new List<EntityUid> { player }, boss), Is.False);
            Assert.That(component.Active, Is.False);
            Assert.That(component.Players, Is.Empty);
            Assert.That(entities.HasComponent<BossEncounterParticipantComponent>(player), Is.False);

            entities.DeleteEntity(boss);
            entities.DeleteEntity(player);
        });
    }

    [Test]
    public async Task OriginalBossEnrollmentAndReturnCannotMoveRevenantParticipants()
    {
        var server = await StartBossServer();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var system = server.System<BossSystem>();
            var arenaCoordinates = CreateTestGrid(server);
            var arenaBoss = entities.SpawnEntity("RevenantBoss", arenaCoordinates);
            var arenaPlayer = entities.SpawnEntity("MobHuman", arenaCoordinates);
            Assert.That(system.TryStartBossfight([arenaPlayer], arenaBoss), Is.True);
            var originalCoordinates = entities.GetComponent<TransformComponent>(arenaPlayer).Coordinates;
            var outside = CreateTestGrid(server);
            var skull = entities.SpawnEntity("SkeletonSkullBoss", outside);
            var skullPlayer = entities.SpawnEntity("MobHuman", outside);
            var marker = entities.SpawnEntity(null, outside);
            entities.EnsureComponent<TagComponent>(marker);
            server.System<TagSystem>().AddTag(marker, "BossSpawnPosition");
            Assert.That(system.TryStartBossfight([skullPlayer, arenaPlayer], skull), Is.True);
            Assert.That(entities.GetComponent<BossComponent>(skull).Players, Is.EqualTo(new[] { skullPlayer }));
            Assert.That(entities.HasComponent<BossLegacyParticipantComponent>(skullPlayer), Is.True);
            Assert.That(entities.HasComponent<BossEncounterParticipantComponent>(skullPlayer), Is.False);
            system.SendPlayersBack(entities.GetComponent<BossComponent>(skull));
            Assert.That(entities.HasComponent<BossLegacyParticipantComponent>(skullPlayer), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(arenaPlayer).Coordinates, Is.EqualTo(originalCoordinates));
            Assert.That(entities.GetComponent<BossEncounterParticipantComponent>(arenaPlayer).Boss, Is.EqualTo(entities.GetNetEntity(arenaBoss)));
            Assert.That(entities.GetComponent<BossHealthBarComponent>(skull).MaxHp, Is.EqualTo(5000));
        });
    }

    private async Task<ServerIntegrationInstance> StartBossServer()
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
            Options = new() { LoadConfigAndUserData = false },
            BeforeStart = () => IoCManager.Resolve<IEntitySystemManager>()
                .LoadExtraSystemType<BossOutcomeListenerSystem>()
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;

        var server = StartServer(options);
        await server.WaitIdleAsync();
        return server;
    }

    private static EntityCoordinates CreateTestGrid(ServerIntegrationInstance server)
    {
        var maps = server.System<SharedMapSystem>();
        maps.CreateMap(out var mapId);
        var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
        maps.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
        maps.SetTile(grid.Owner, grid.Comp, new Vector2i(1, 0), new Tile(1));
        return new EntityCoordinates(grid.Owner, 0, 0);
    }

    [Reflect(false)]
    public sealed class BossOutcomeListenerSystem : EntitySystem
    {
        public readonly List<EntityUid> Defeated = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BossDefeatedEvent>(OnDefeated);
            SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Defeated.Clear());
        }

        private void OnDefeated(ref BossDefeatedEvent args)
        {
            Defeated.Add(args.Boss);
        }
    }
}
