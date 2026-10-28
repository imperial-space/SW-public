#nullable enable

using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Chasm;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantArenaMapTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task ArenaLoadsWithExactSvgWallAndSeparateCombatFloor()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false, ContentStart = true, LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false }
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        EntityUid mapUid = default;
        MapId mapId = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"), out var map, out var grids), Is.True);
            Assert.That(grids, Has.Count.EqualTo(2));
            mapUid = map!.Value.Owner;
            mapId = map.Value.Comp.MapId;

            var atmosphere = server.System<AtmosphereSystem>();
            foreach (var grid in grids!)
            {
                var tiles = server.System<SharedMapSystem>().GetAllTiles(grid,
                    server.EntMan.GetComponent<MapGridComponent>(grid)).ToArray();
                var mixtures = atmosphere.GetAllMixtures(grid).ToArray();
                Assert.That(mixtures, Has.Length.EqualTo(tiles.Length), "Every tile must have preloaded air.");
                foreach (var air in mixtures)
                {
                    Assert.That(air.Immutable, Is.False, "Normal grid atmosphere must still simulate.");
                    AssertSafeAir(atmosphere, air);
                }
            }

            server.System<SharedMapSystem>().InitializeMap(mapId);
            server.System<SharedMapSystem>().SetPaused(mapId, false);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var transforms = server.System<SharedTransformSystem>();
            var crystal = entities.AllEntities<BossSummoningTriggerComponent>()
                .Single(ent => entities.GetComponent<TransformComponent>(ent.Owner).MapID == mapId);
            var bossTransform = entities.GetComponent<TransformComponent>(crystal.Owner);
            var platform = bossTransform.GridUid!.Value;
            var platformGrid = entities.GetComponent<MapGridComponent>(platform);
            var tiles = maps.GetAllTiles(platform, platformGrid).Select(tile => tile.GridIndices).ToHashSet();
            Assert.That(tiles, Has.Count.EqualTo(121));
            var definitions = server.ResolveDependency<ITileDefinitionManager>();
            foreach (var tile in maps.GetAllTiles(platform, platformGrid))
            {
                var definition = (ContentTileDefinition) definitions[tile.Tile.TypeId];
                var row = 6 - tile.GridIndices.Y;
                var column = tile.GridIndices.X + 6;
                Assert.That(definition.ID, Is.EqualTo($"RevenantBossArenaFloorR{row:00}C{column:00}"));
                Assert.That(definition.Sprite, Is.EqualTo(new ResPath(
                    $"/Textures/Imperial/Medieval/Tiles/RevenantArena/floor/floor_r{row:00}_c{column:00}.png")));
                Assert.That(definition.Variants, Is.EqualTo(1));
                Assert.That(definition.AllowRotationMirror, Is.False);
                Assert.That(tile.Tile.Variant, Is.Zero);
                Assert.That(definition.Indestructible, Is.True);
                Assert.That(definition.Weather, Is.False);
            }
            for (var x = -5; x <= 5; x++)
            for (var y = -5; y <= 5; y++)
                Assert.That(tiles, Does.Contain(new Vector2i(x, y)));
            Assert.That(transforms.GetWorldPosition(bossTransform), Is.EqualTo(new Vector2(0.5f, 0.5f)));
            var center = entities.AllEntities<BossArenaCenterBlockerComponent>()
                .Single(ent => entities.GetComponent<TransformComponent>(ent.Owner).MapID == mapId);
            var centerTransform = entities.GetComponent<TransformComponent>(center.Owner);
            Assert.That(centerTransform.ParentUid, Is.EqualTo(platform));
            Assert.That(centerTransform.Anchored, Is.True);
            Assert.That(transforms.GetWorldPosition(centerTransform), Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(entities.HasComponent<Content.Shared.Damage.DamageableComponent>(center.Owner), Is.False);
            Assert.That(entities.GetComponent<FixturesComponent>(center.Owner).Fixtures.Values.Single().Hard, Is.True);
            Assert.That(entities.AllEntities<BossComponent>(), Is.Empty);
            Assert.That(entities.GetComponent<BossArenaMapComponent>(mapUid).Crystal, Is.EqualTo(crystal.Owner));
            var braziers = entities.GetComponent<BossArenaMapComponent>(mapUid).Braziers;
            Assert.That(braziers, Has.Count.EqualTo(4));
            foreach (var brazier in braziers)
                Assert.That(entities.HasComponent<FixturesComponent>(brazier), Is.False, "Brazier cues must not obstruct a safe square.");

            var artwork = entities.AllEntities<MetaDataComponent>()
                .Where(ent => ent.Comp.EntityPrototype?.ID is "RevenantBossArenaEdges" or
                                  "RevenantBossBrazierFloorMount" or "RevenantBossArenaRune")
                .Where(ent => entities.GetComponent<TransformComponent>(ent.Owner).MapID == mapId).ToArray();
            Assert.That(artwork, Has.Length.EqualTo(6));
            foreach (var decoration in artwork)
            {
                var transform = entities.GetComponent<TransformComponent>(decoration.Owner);
                Assert.That(transform.GridUid, Is.EqualTo(platform));
                Assert.That(transform.Anchored, Is.True);
                Assert.That(entities.HasComponent<BossArenaProtectedComponent>(decoration.Owner), Is.True);
                Assert.That(entities.HasComponent<FixturesComponent>(decoration.Owner), Is.False,
                    "The visible edge and ground decals must not add collision or walkable tiles.");
            }
            var mounts = artwork.Where(ent => ent.Comp.EntityPrototype!.ID == "RevenantBossBrazierFloorMount").ToArray();
            Assert.That(mounts, Has.Length.EqualTo(4));
            Assert.That(mounts.Select(ent => transforms.GetWorldPosition(entities.GetComponent<TransformComponent>(ent.Owner))),
                Is.EquivalentTo(braziers.Select(uid => transforms.GetWorldPosition(entities.GetComponent<TransformComponent>(uid)))));
            var rune = artwork.Single(ent => ent.Comp.EntityPrototype!.ID == "RevenantBossArenaRune");
            Assert.That(transforms.GetWorldPosition(entities.GetComponent<TransformComponent>(rune.Owner)),
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(definitions.TryGetDefinition("RevenantBossArenaRune", out _), Is.False,
                "The central purple mark is a separate entity, never a floor tile.");

            var wallEntities = entities.AllEntities<MetaDataComponent>()
                .Where(ent => ent.Comp.EntityPrototype?.ID == "RevenantBossArenaWall" &&
                              entities.GetComponent<TransformComponent>(ent.Owner).MapID == mapId).ToArray();
            Assert.That(wallEntities, Has.Length.EqualTo(848));
            var scenery = entities.GetComponent<TransformComponent>(wallEntities[0].Owner).GridUid!.Value;
            Assert.That(scenery, Is.Not.EqualTo(platform));
            var wallPositions = wallEntities.Select(ent =>
            {
                var xform = entities.GetComponent<TransformComponent>(ent.Owner);
                Assert.That(xform.GridUid, Is.EqualTo(scenery));
                Assert.That(xform.Anchored, Is.True);
                Assert.That(entities.GetComponent<FixturesComponent>(ent.Owner).Fixtures.Values.Any(f => f.Hard), Is.True);
                var position = transforms.GetWorldPosition(xform);
                return new Vector2i((int) (position.X - 0.5f), (int) (position.Y - 0.5f));
            }).OrderBy(pos => pos.X).ThenBy(pos => pos.Y).ToArray();

            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join("\n", wallPositions.Select(pos => $"{pos.X},{pos.Y}")))));
            Assert.That(fingerprint, Is.EqualTo("32B3A48EF1EA6D01099D542E605C28C775A0B05C6592F34EB0E8B9B86B082C58"));
            for (var y = 25; y <= 28; y++)
                Assert.That(wallPositions, Does.Contain(new Vector2i(0, y)), "The wall is four tiles thick at its north edge.");
            var chasms = entities.AllEntities<BossArenaChasmComponent>().ToArray();
            Assert.That(chasms, Has.Length.EqualTo(1732));
            var chasmPositions = chasms.Select(ent =>
            {
                Assert.That(entities.HasComponent<ChasmComponent>(ent.Owner), Is.False, "Arena pits must not delete bodies.");
                var xform = entities.GetComponent<TransformComponent>(ent.Owner);
                Assert.That(xform.GridUid, Is.EqualTo(scenery));
                Assert.That(xform.Anchored, Is.True);
                var position = transforms.GetWorldPosition(xform);
                return new Vector2i((int) (position.X - 0.5f), (int) (position.Y - 0.5f));
            }).ToArray();
            var backing = maps.GetAllTiles(scenery, entities.GetComponent<MapGridComponent>(scenery))
                .Select(tile => tile.GridIndices).ToHashSet();
            Assert.That(backing, Is.EquivalentTo(wallPositions.Concat(chasmPositions)));
            Assert.That(backing.Intersect(tiles), Is.Empty);

            var entrance = entities.AllEntities<BossArenaEntranceComponent>()
                .Single(ent => entities.GetComponent<TransformComponent>(ent.Owner).MapID == mapId);
            var entranceTransform = entities.GetComponent<TransformComponent>(entrance.Owner);
            Assert.That(entranceTransform.ParentUid, Is.EqualTo(platform));
            var arrival = transforms.GetWorldPosition(entranceTransform) + entrance.Comp.ArrivalOffset;
            Assert.That(arrival, Is.EqualTo(new Vector2(0.5f, -3)));
            var manager = server.ResolveDependency<IMapManager>();
            Assert.That(manager.TryFindGridAt(mapId, arrival, out var arrivalGrid, out _), Is.True);
            Assert.That(arrivalGrid, Is.EqualTo(platform));
            Assert.That(manager.TryFindGridAt(mapId, new Vector2(0.5f, -6.5f), out var pitGrid, out _), Is.True);
            Assert.That(pitGrid, Is.EqualTo(scenery), "Pit backing must never enlarge the combat grid.");
            Assert.That(entities.GetComponent<GravityComponent>(mapUid).Enabled, Is.True);
            Assert.That(entities.GetComponent<MapAtmosphereComponent>(mapUid).Space, Is.False);
            var atmosphere = server.System<AtmosphereSystem>();
            AssertSafeAir(atmosphere, entities.GetComponent<MapAtmosphereComponent>(mapUid).Mixture);
            foreach (var tile in tiles)
                AssertSafeAir(atmosphere, atmosphere.GetTileMixture(platform, mapUid, tile));
            foreach (var tile in chasmPositions)
                AssertSafeAir(atmosphere, atmosphere.GetTileMixture(scenery, mapUid, tile));
        });
    }

    private static void AssertSafeAir(AtmosphereSystem atmosphere, GasMixture? air)
    {
        Assert.That(air, Is.Not.Null);
        Assert.That(atmosphere.IsMixtureProbablySafe(air), Is.True);
        Assert.That(air!.Temperature, Is.EqualTo(Atmospherics.T20C).Within(0.01));
        Assert.That(air.Pressure, Is.EqualTo(Atmospherics.OneAtmosphere).Within(0.01));
        Assert.That(air.GetMoles(Gas.Oxygen), Is.EqualTo(Atmospherics.OxygenMolesStandard).Within(0.01));
        Assert.That(air.GetMoles(Gas.Nitrogen), Is.EqualTo(Atmospherics.NitrogenMolesStandard).Within(0.01));
        Assert.That(air.TotalMoles - air.GetMoles(Gas.Oxygen) - air.GetMoles(Gas.Nitrogen), Is.EqualTo(0).Within(0.001));
    }
}
