using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Maps;
using Content.Shared.StatusEffectNew;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tiles;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantOutsideRegressionTest : RevenantIntegrationTestBase
{
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public async Task OrdinaryInstantAndInHandSpellsKeepSpawningAndManaCosts(int arenaState, bool inHand)
    {
        var (server, grid, caster, _) = await Setup(arenaState);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coordinates = entities.GetComponent<TransformComponent>(caster).Coordinates;
            var action = entities.SpawnEntity(inHand ? "MedievalActionIceDaggerBeginner" : "MedievalActionHolyTouchBeginner", coordinates);
            var mana = entities.EnsureComponent<ManaComponent>(caster);
            mana.MaxMana = 1000;
            mana.Mana = 500;
            var cost = entities.GetComponent<ManaDrainSpellComponent>(action).ManaDrain;
            var prototype = inHand ? "IceDaggerBeginner" : "HolyTouch";
            MedievalInstantSpellEvent cast = inHand
                ? new MedievalSpawnInHandSpellEvent { SpawnedEntityPrototype = prototype }
                : new MedievalInstantSpawnEvent { SpawnedEntityPrototype = prototype };
            cast.Action = action;
            cast.Performer = caster;
            if (cast is MedievalSpawnInHandSpellEvent handCast)
                entities.EventBus.RaiseEvent(EventSource.Local, handCast);
            else
                entities.EventBus.RaiseEvent(EventSource.Local, (MedievalInstantSpawnEvent)cast);

            Assert.That(cast.Handled, Is.True);
            var spawned = entities.AllEntities<MetaDataComponent>()
                .Single(entity => entity.Comp.EntityPrototype?.ID == prototype).Owner;
            Assert.That(entities.GetComponent<TransformComponent>(spawned).MapUid,
                Is.EqualTo(entities.GetComponent<TransformComponent>(caster).MapUid));
            Assert.That(entities.HasComponent<BossArenaSpellSourceComponent>(spawned), Is.False);
            Assert.That(mana.Mana, Is.EqualTo(500 - cost));
            Assert.That(mana.CastedSpells, Is.Empty);
            Assert.That(server.System<SharedBossArenaRulesSystem>().CurrentSource, Is.Null);
            if (inHand)
            {
                Assert.That(server.System<SharedHandsSystem>().TryGetActiveItem(caster, out var held), Is.True);
                Assert.That(held, Is.EqualTo(spawned));
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task OrdinaryDamageFireAndStunsWorkBeforeDuringAndAfterArenaLifetime(int arenaState)
    {
        var (server, grid, attacker, target) = await Setup(arenaState);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            Assert.That(rules.IsArena(target), Is.False);
            var hurt = new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(20) } };
            server.System<DamageableSystem>().TryChangeDamage(target, hurt, origin: attacker);
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.EqualTo(20));
            server.System<DamageableSystem>().TryChangeDamage(target, -hurt, origin: attacker);
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.Zero);

            entities.EnsureComponent<BossArenaProtectedComponent>(target);
            entities.EnsureComponent<BossArenaPartyMemberComponent>(target);
            using (rules.WithSource(attacker))
            {
                Assert.That(rules.CurrentSource, Is.Null);
                Assert.That(server.System<SharedStunSystem>().TryAddStunDuration(target, TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(server.System<StatusEffectsSystem>().CanAddStatusEffect(target, SharedStunSystem.StunId), Is.True);
                server.System<FlammableSystem>().SetFireStacks(target, 2);
                server.System<FlammableSystem>().Ignite(target, attacker, ignitionSourceUser: attacker);
                Assert.That(entities.GetComponent<FlammableComponent>(target).OnFire, Is.True);
            }
            Assert.That(rules.CurrentSource, Is.Null);
            Assert.That(rules.BlocksControl(target, attacker), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task OrdinaryStorageFurnitureAndTerrainWorkBeforeDuringAndAfterArenaLifetime(int arenaState)
    {
        var (server, grid, attacker, target) = await Setup(arenaState);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var rules = server.System<SharedBossArenaRulesSystem>();
            var coordinates = new EntityCoordinates(grid.Owner, 0.5f, 0.5f);
            entities.EnsureComponent<BossArenaProtectedComponent>(target);
            entities.EnsureComponent<BossArenaPartyMemberComponent>(target);
            var locker = entities.SpawnEntity("LockerSteel", coordinates);
            Assert.That(server.System<SharedEntityStorageSystem>().CanInsert(target, locker), Is.True);
            var container = server.System<SharedContainerSystem>().EnsureContainer<Container>(locker, "outside-regression");
            Assert.That(server.System<SharedContainerSystem>().Insert(target, container), Is.True);
            Assert.That(server.System<SharedContainerSystem>().Remove(target, container), Is.True);
            var chair = entities.SpawnEntity("ChairFoldingSpawnFolded", new EntityCoordinates(grid.Owner, 2.5f, 2.5f));
            Assert.That(server.System<FoldableSystem>().TrySetFolded(chair, entities.GetComponent<FoldableComponent>(chair), false), Is.True);
            Assert.That(entities.GetComponent<FoldableComponent>(chair).IsFolded, Is.False);
            Assert.That(rules.CanSpawn("MedievalIceBarrierBeginner", coordinates), Is.True);
            Assert.That(rules.CanSpawn("MedievalStoneRidgeMiddle", coordinates), Is.True);
            Assert.That(server.System<FloorTileSystem>().CanPlaceTile(grid.Owner, grid.Comp, new Vector2i(3, 3), out _), Is.True);
            var tiles = server.ResolveDependency<ITileDefinitionManager>();
            var tile = new Vector2i(-3, -3);
            server.System<SharedMapSystem>().SetTile(grid.Owner, grid.Comp, tile, new Tile(tiles["FloorSteel"].TileId));
            Assert.That(server.System<TileSystem>().DeconstructTile(grid.Comp.GetTileRef(tile)), Is.True);
        });
    }

    private async Task<(ServerIntegrationInstance Server, Entity<MapGridComponent> Grid, EntityUid Attacker, EntityUid Target)> Setup(int arenaState)
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
        Entity<MapGridComponent> grid = default;
        EntityUid attacker = default, target = default;
        await server.WaitAssertion(() =>
        {
            if (arenaState > 0)
            {
                Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                    out var arena, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
                if (arenaState == 2) server.EntMan.DeleteEntity(arena!.Value.Owner);
            }
            server.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var x = -4; x <= 4; x++) for (var y = -4; y <= 4; y++)
                server.System<SharedMapSystem>().SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            attacker = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            target = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid.Owner, 1.5f, 0.5f));
            foreach (var body in new[] { attacker, target })
            {
                server.EntMan.RemoveComponent<Content.Shared.Imperial.Medieval.Additions.ShieldOnStartupComponent>(body);
                server.EntMan.RemoveComponent<Content.Shared.Damage.Components.PassiveDamageComponent>(body);
            }
        });
        return (server, grid, attacker, target);
    }
}
