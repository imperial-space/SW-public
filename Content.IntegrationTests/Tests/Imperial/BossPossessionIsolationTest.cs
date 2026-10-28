using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;

using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossPossessionIsolationTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task RevenantUnderstandsSpeaksAndOffersEveryRegisteredLanguage()
    {
        var server = await Start();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = entities.SpawnEntity("RevenantBoss", Grid(server));
            var languages = server.System<SharedLanguageSystem>();
            var all = server.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<LanguagePrototype>().ToArray();
            Assert.That(all.Length, Is.GreaterThan(1));
            Assert.That(languages.GetLanguagesKnowledged(boss, LanguageKnowledge.Speak, out var menu, out var current), Is.True);
            Assert.That(current, Is.EqualTo("Universal"));
            Assert.That(menu.Keys, Is.EquivalentTo(all.Select(language => language.ID)));
            foreach (var language in all)
            {
                Assert.That(languages.CanSpeak(boss, language.ID), Is.True, language.ID);
                Assert.That(languages.CanUnderstand(boss, language.ID), Is.True, language.ID);
                entities.GetComponent<LanguageSpeakerComponent>(boss).CurrentLanguage = language.ID;
                Assert.That(languages.GetCurrentLanguage(boss).ID, Is.EqualTo(language.ID));
            }
            var ordinary = entities.SpawnEntity(null, Grid(server));
            var speaker = entities.EnsureComponent<LanguageSpeakerComponent>(ordinary);
            speaker.CurrentLanguage = "Common";
            speaker.Languages["Common"] = LanguageKnowledge.Speak;
            entities.EnsureComponent<UniversalLanguageSpeakerComponent>(ordinary);
            Assert.That(languages.GetLanguagesKnowledged(ordinary, LanguageKnowledge.Speak, out var originalMenu, out var originalCurrent), Is.True);
            Assert.That(originalMenu.Keys, Is.EquivalentTo(new[] { "Common" }));
            Assert.That(originalCurrent, Is.EqualTo("Common"));
            foreach (var language in all)
                Assert.That(languages.CanSpeak(ordinary, language.ID), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PossessionAndReleaseKeepBossDamageableAndOutsideParty(bool visiting)
    {
        var server = await Start();
        var sessions = await server.AddDummySessions(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var arena = Arena(server);
            var boss = arena.Comp.Boss;
            var player = Player(server, arena);
            var administrator = entities.SpawnEntity("MobHuman", Grid(server));
            var minds = server.System<SharedMindSystem>();
            var mind = minds.CreateMind(sessions[0].UserId);
            minds.TransferTo(mind, administrator);
            if (visiting) minds.Visit(mind, boss);
            else minds.TransferTo(mind, boss);

            server.PlayerMan.SetAttachedEntity(sessions[0], boss);
            var rules = server.System<SharedBossArenaRulesSystem>();
            Assert.That(entities.HasComponent<BossActorComponent>(boss), Is.True);
            entities.RemoveComponent<BossArenaAttackSourceComponent>(boss);
            var bosses = server.System<BossSystem>();
            var damage = server.System<DamageableSystem>();
            Assert.That(rules.IsPartyBody(boss), Is.False);
            Assert.That(server.System<BossArenaMapSystem>().GetParty(arena.Owner), Is.EqualTo(new[] { player }));
            Assert.That(rules.GetSource(boss), Is.Null);
            Assert.That(rules.BlocksFriendlyEffect(boss, player), Is.False);
            Assert.That(bosses.TryStartBossfight([player, boss], boss), Is.True);
            Assert.That(entities.GetComponent<BossComponent>(boss).Players, Is.EqualTo(new[] { player }),
                "An explicitly supplied possessed boss cannot enroll as its own participant.");
            Assert.That(entities.GetComponent<BossHealthScalingComponent>(boss).PlayerCount, Is.Zero,
                "The administrator controlling the boss cannot inflate participant health scaling.");
            var before = entities.GetComponent<DamageableComponent>(boss).TotalDamage;
            damage.TryChangeDamage(boss, Hit(25), ignoreResistances: true, origin: player);
            Assert.That(entities.GetComponent<DamageableComponent>(boss).TotalDamage, Is.EqualTo(before + FixedPoint2.New(25)));
            Assert.That(server.System<BossTelegraphSystem>().LivingPlayers(boss), Is.EqualTo(new[] { player }));
            using (rules.WithSource(player))
                Assert.That(rules.BlocksFriendlyEffect(player, boss), Is.False,
                    "Possessed boss damage cannot inherit a surrounding party spell scope.");
            if (visiting) minds.UnVisit(mind);
            else minds.TransferTo(mind, administrator);
            server.PlayerMan.SetAttachedEntity(sessions[0], administrator);
            Assert.That(rules.IsPartyBody(boss), Is.False);
            damage.TryChangeDamage(boss, Hit(25), ignoreResistances: true, origin: player);
            Assert.That(entities.GetComponent<DamageableComponent>(boss).TotalDamage, Is.EqualTo(before + FixedPoint2.New(50)));
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, Attack(BossTelegraphPattern.Cross)), Is.True,
                "After the administrator leaves, the living encounter can still execute its next mechanic.");
            mechanics.Cancel(boss);
            bosses.DamageBoss(boss, 1000000);
            Assert.That(entities.GetComponent<BossComponent>(boss).Finished, Is.True);
            Assert.That(entities.GetComponent<BossEncounterComponent>(boss).FightRunning, Is.False);
            Assert.That(arena.Comp.VictoryExit, Is.Not.Null, "Possession cannot block ordinary victory processing.");
        });
    }

    [Test]
    public async Task ConcurrentRevenantsKeepMechanicsGatesWarningsAndVictoryIndependent()
    {
        var server = await Start();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var a = Arena(server);
            var b = Arena(server);
            var pa = Player(server, a);
            var pb = Player(server, b);
            var outside = Grid(server);
            var ga = entities.SpawnEntity("RevenantBossGate", outside);
            var gb = entities.SpawnEntity("RevenantBossGate", outside.Offset(new(3, 0)));
            var gates = server.System<BossArenaGateSystem>();
            Assert.That(gates.LinkGate(ga, a.Comp.Entrance), Is.True);
            Assert.That(gates.LinkGate(gb, b.Comp.Entrance), Is.True);
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight([pa], a.Comp.Boss), Is.True);
            Assert.That(bosses.TryStartBossfight([pb], b.Comp.Boss), Is.True);
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(a.Comp.Boss, Attack(BossTelegraphPattern.BlackoutNorthWest)), Is.True);
            Assert.That(mechanics.TryStart(b.Comp.Boss, Attack(BossTelegraphPattern.BlackoutSouthEast)), Is.True);
            var other = entities.GetComponent<BossMechanicComponent>(b.Comp.Boss);
            var deadline = other.Deadline;
            var generation = entities.GetComponent<BossEncounterComponent>(b.Comp.Boss).Generation;
            var warning = entities.AllEntities<BossTelegraphComponent>()
                .Single(entity => entity.Comp.Boss == b.Comp.Boss && !entity.Comp.Impact).Owner;
            var gateState = entities.GetComponent<BossArenaGateComponent>(gb);
            var revision = gateState.Revision;
            Assert.That(other.Blackout, Is.True);
            mechanics.Cancel(a.Comp.Boss);
            bosses.DamageBoss(a.Comp.Boss, 1000000);
            Assert.That(a.Comp.VictoryExit, Is.Not.Null);
            Assert.That(b.Comp.VictoryExit, Is.Null);
            Assert.That(entities.GetComponent<BossMechanicComponent>(b.Comp.Boss), Is.SameAs(other));
            Assert.That(other.Deadline, Is.EqualTo(deadline));
            Assert.That(entities.GetComponent<BossEncounterComponent>(b.Comp.Boss).Generation, Is.EqualTo(generation));
            Assert.That(entities.EntityExists(warning), Is.True);
            Assert.That(gateState.Revision, Is.EqualTo(revision));
            Assert.That(gateState.CombatLocked, Is.True);
            Assert.That(entities.GetComponent<BossComponent>(b.Comp.Boss).Active, Is.True);
            Assert.That(server.System<BossTelegraphSystem>().LivingPlayers(b.Comp.Boss), Is.EqualTo(new[] { pb }));
            server.System<SharedMapSystem>().DeleteMap(entities.GetComponent<TransformComponent>(a.Owner).MapID);
            Assert.That(entities.GetComponent<BossMechanicComponent>(b.Comp.Boss), Is.SameAs(other));
            Assert.That(entities.EntityExists(warning), Is.True);
            Assert.That(gateState.Destination, Is.EqualTo(b.Comp.Entrance));
            Assert.That(gateState.CombatLocked, Is.True);
        });
    }

    [Test]
    public async Task TwoLegacyBossesKeepParticipantsReturnsAndAttackCooldownsSeparate()
    {
        var server = await Start();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var arena = Grid(server);
            var outside = Grid(server);
            var foreignReturn = entities.SpawnEntity(null, arena.Offset(new(3, 3)));
            entities.EnsureComponent<BossPlayersBackPointComponent>(foreignReturn);
            var unstaged = entities.SpawnEntity(null, outside);
            var unstagedBoss = entities.EnsureComponent<BossComponent>(unstaged);
            var originalHealth = unstagedBoss.Health;
            server.System<BossSystem>().DamageBoss(unstaged, 1);
            Assert.That(unstagedBoss.Health, Is.EqualTo(originalHealth - 1));
            Assert.That(unstagedBoss.Stage, Is.EqualTo(1));
            server.System<BossSystem>().DamageBoss(unstaged, -1);
            Assert.That(unstagedBoss.Health, Is.EqualTo(originalHealth));
            var marker = entities.SpawnEntity(null, arena);
            entities.EnsureComponent<TagComponent>(marker);
            server.System<TagSystem>().AddTag(marker, "BossSpawnPosition");
            var a = entities.SpawnEntity("SkeletonSkullBoss", arena);
            var b = entities.SpawnEntity("SkeletonSkullBoss", arena.Offset(new(4, 0)));
            var pa = entities.SpawnEntity("MobHuman", outside);
            var pb = entities.SpawnEntity("MobHuman", outside.Offset(new(2, 0)));
            var bosses = server.System<BossSystem>();
            bosses.StartBossfight([pa], a);
            Assert.That(entities.GetComponent<BossComponent>(a).Active, Is.True);
            Assert.That(bosses.TryStartBossfight([pb, pa], b), Is.True);
            Assert.That(entities.GetComponent<FightingBossComponent>(pa).Boss, Is.EqualTo(entities.GetNetEntity(a)));
            Assert.That(entities.GetComponent<FightingBossComponent>(pb).Boss, Is.EqualTo(entities.GetNetEntity(b)));
            var barA = entities.GetComponent<BossHealthBarComponent>(a);
            var barB = entities.GetComponent<BossHealthBarComponent>(b);
            Assert.That(barA.Active, Is.True);
            Assert.That(barA.MaxHp, Is.EqualTo(5000));
            bosses.DamageBoss(a, 25);
            server.System<BossHealthBarServerSystem>().SyncBossHealth(a, entities.GetComponent<BossComponent>(a), barA);
            Assert.That(barA.CurrentHp, Is.EqualTo(4975));
            Assert.That(barB.CurrentHp, Is.EqualTo(5000));
            Assert.That(entities.GetComponent<BossComponent>(b).Players, Is.EqualTo(new[] { pb }));
            var attackA = entities.GetComponent<BossComponent>(a).Stages[1].Attacks[0];
            var attackB = entities.GetComponent<BossComponent>(b).Stages[1].Attacks[0];
            Assert.That(attackA, Is.Not.SameAs(attackB));
            var before = attackB.NextAttack;
            attackA.NextAttack = TimeSpan.FromSeconds(1234);
            Assert.That(attackB.NextAttack, Is.EqualTo(before));
            var positionB = entities.GetComponent<TransformComponent>(pb).Coordinates;
            var aBoss = entities.GetComponent<BossComponent>(a);
            server.System<MobStateSystem>().ChangeMobState(pa, MobState.Dead);
            aBoss.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
            bosses.Update(0);
            Assert.That(entities.IsQueuedForDeletion(a), Is.False);
            Assert.That(aBoss.ReturnPlayersTime, Is.EqualTo(server.ResolveDependency<IGameTiming>().CurTime + aBoss.SendBackDelay));
            Assert.That(barA.Active, Is.False);
            Assert.That(entities.HasComponent<BossLegacyParticipantComponent>(pa), Is.True);
            aBoss.ReturnPlayersTime = server.ResolveDependency<IGameTiming>().CurTime;
            bosses.Update(0);
            Assert.That(entities.GetComponent<TransformComponent>(pa).Coordinates, Is.EqualTo(outside));
            Assert.That(entities.HasComponent<BossLegacyParticipantComponent>(pa), Is.False);
            Assert.That(entities.HasComponent<FightingBossComponent>(pa), Is.False);
            Assert.That(entities.HasComponent<FightingBossComponent>(pb), Is.True);
            Assert.That(entities.GetComponent<BossLegacyParticipantComponent>(pb).Boss, Is.EqualTo(b));
            Assert.That(entities.GetComponent<TransformComponent>(pb).Coordinates, Is.EqualTo(positionB));
            entities.DeleteEntity(a);
            Assert.That(entities.GetComponent<BossLegacyParticipantComponent>(pb).Boss, Is.EqualTo(b));
            Assert.That(entities.GetComponent<BossComponent>(b).Active, Is.True);
        });
    }

    [Test]
    public async Task LegacyAttackOwnershipExistsBeforeTrapMapInitAndHealingIsScopedToItsBoss()
    {
        var server = await Start();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var arena = Grid(server);
            var outside = Grid(server);
            var marker = entities.SpawnEntity(null, arena);
            entities.EnsureComponent<TagComponent>(marker);
            server.System<TagSystem>().AddTag(marker, "BossSpawnPosition");
            var a = entities.SpawnEntity("SkeletonSkullBoss", arena);
            var b = entities.SpawnEntity("SkeletonSkullBoss", arena.Offset(new(4, 0)));
            var partyA = Enumerable.Range(0, 3).Select(_ => entities.SpawnEntity("MobHuman", outside)).ToArray();
            var partyB = Enumerable.Range(0, 2).Select(_ => entities.SpawnEntity("MobHuman", outside)).ToArray();
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight(partyA.ToList(), a), Is.True);
            Assert.That(bosses.TryStartBossfight(partyB.ToList(), b), Is.True);
            new SpawnAtPosition { Prototypes = ["SkullBossCrystalTrap"], SpawnCount = 1 }.Execute(a, new[] { a }, entities);
            var trap = entities.AllEntities<TrapPlayersOnMapComponent>().Single();
            Assert.That(entities.GetComponent<BossAttackComponent>(trap).Boss, Is.EqualTo(a));
            Assert.That(trap.Comp.Trapped, Has.Count.EqualTo(1));
            Assert.That(partyA, Does.Contain(trap.Comp.Trapped.Single()));
            foreach (var player in partyB)
                Assert.That(entities.GetComponent<TransformComponent>(player).GridUid,
                    Is.EqualTo(entities.GetComponent<TransformComponent>(b).GridUid));
            var trappedPlayer = trap.Comp.Trapped.Single();
            var trapMap = trap.Comp.Map;
            entities.DeleteEntity(trap.Owner);
            Assert.That(entities.EntityExists(trapMap), Is.False);
            Assert.That(entities.HasComponent<BossTrappedComponent>(trappedPlayer), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(trappedPlayer).GridUid,
                Is.EqualTo(entities.GetComponent<TransformComponent>(a).GridUid));
            bosses.DamageBoss(a, 100);
            bosses.DamageBoss(b, 100);
            new SpawnAtPosition { Prototypes = ["MobBossHealingSkull"], SpawnCount = 1 }.Execute(a, new[] { a }, entities);
            new SpawnAtPosition { Prototypes = ["MobBossHealingSkull"], SpawnCount = 1 }.Execute(b, new[] { b }, entities);
            var healer = entities.AllEntities<TimedBossHealComponent>()
                .Single(entity => entities.GetComponent<BossAttackComponent>(entity).Boss == a);
            var bHealth = entities.GetComponent<BossComponent>(b).Health;
            var aHealth = entities.GetComponent<BossComponent>(a).Health;
            entities.EventBus.RaiseLocalEvent(healer.Owner, new BossHealDoAfterEvent
                { DoAfter = entities.GetComponent<DoAfterComponent>(healer).DoAfters.Values.Single() });
            Assert.That(entities.GetComponent<BossComponent>(a).Health, Is.EqualTo(aHealth + 45));
            Assert.That(entities.GetComponent<BossComponent>(b).Health, Is.EqualTo(bHealth));
            Assert.That(server.System<SharedAppearanceSystem>().TryGetData(a, AdditionalBossVisuals.State, out string state), Is.True);
            Assert.That(state, Is.EqualTo("healing"), "Another boss's pending heal cannot suppress this boss's completion effect.");
        });
    }

    private async Task<ServerIntegrationInstance> Start()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false, ContentStart = true, LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (name, value) in PoolManager.TestCvars) options.CVarOverrides[name] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        return server;
    }

    private static EntityCoordinates Grid(ServerIntegrationInstance server)
    {
        var maps = server.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: true);
        var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
        for (var x = -5; x <= 5; x++) for (var y = -5; y <= 5; y++)
            maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
        return new(grid.Owner, 0.5f, 0.5f);
    }

    private static Entity<BossArenaMapComponent> Arena(ServerIntegrationInstance server)
    {
        Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
            out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
        var entities = server.EntMan;
        var arena = new Entity<BossArenaMapComponent>(map!.Value.Owner, entities.GetComponent<BossArenaMapComponent>(map.Value.Owner));
        server.System<SharedMapSystem>().SetPaused(map.Value.Comp.MapId, false);
        if (arena.Comp.Crystal is { } crystal) entities.DeleteEntity(crystal);
        arena.Comp.Crystal = null;
        arena.Comp.Boss = entities.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
        entities.GetComponent<BossEncounterComponent>(arena.Comp.Boss).TankDamage = new DamageSpecifier();
        return arena;
    }

    private static EntityUid Player(ServerIntegrationInstance server, Entity<BossArenaMapComponent> arena)
    {
        var uid = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
        server.EntMan.RemoveComponent<ShieldOnStartupComponent>(uid);
        server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(uid);
        return uid;
    }

    private static DamageSpecifier Hit(int amount) => new() { DamageDict = { ["Blunt"] = FixedPoint2.New(amount) } };
    private static BossTilePatternAttack Attack(BossTelegraphPattern pattern)
        => new() { Telegraph = pattern, CastTime = 10, Damage = Hit(1), FollowupDelay = 1 };
}
