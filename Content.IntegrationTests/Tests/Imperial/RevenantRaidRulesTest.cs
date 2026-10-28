using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Medieval.Boss;
using Content.Server.Body.Components;
using Content.Server.Imperial.Medieval.Magic;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.ImperialStore;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed partial class RevenantRaidRulesTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task RaidTankMovesToFirstRowWithoutReorderingTheOtherPlayers()
    {
        var (server, arena, caster, target) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, Vector2.Zero));
            arena.Comp.Boss = boss;
            var focus = entities.EnsureComponent<BossFocusMarkerComponent>(boss);
            var hud = server.System<BossRaidHudSystem>();
            var original = hud.GetMembers(arena.Owner).Select(member => member.Entity).ToArray();
            focus.Tank = target;
            var firstTank = hud.GetMembers(arena.Owner).Select(member => member.Entity).ToArray();
            Assert.That(firstTank.First(), Is.EqualTo(entities.GetNetEntity(target)));
            Assert.That(firstTank.Skip(1), Is.EqualTo(original.Where(entity => entity != entities.GetNetEntity(target))));
            focus.Tank = caster;
            Assert.That(hud.GetMembers(arena.Owner).First().Entity, Is.EqualTo(entities.GetNetEntity(caster)));
            focus.Tank = null;
            Assert.That(hud.GetMembers(arena.Owner).Select(member => member.Entity), Is.EqualTo(original));
        });
    }

    [Test]
    public async Task EightBodiesFillArenaIncludingDeadAndDisconnectedPlayersAndLeavingFreesSlot()
    {
        var (server, arena, caster, target) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            for (var i = 0; i < 6; i++)
            {
                var body = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 3.5f));
                entities.EnsureComponent<BossArenaPartyMemberComponent>(body);
            }
            server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            var system = server.System<BossArenaMapSystem>();
            var outsider = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(system.GetParty(arena.Owner).Count, Is.EqualTo(8));
            Assert.That(system.HasEntrySpace(arena.Comp.Entrance, outsider), Is.False);
            var members = server.System<BossRaidHudSystem>().GetMembers(arena.Owner);
            Assert.That(members.Count, Is.EqualTo(8));
            Assert.That(members.Single(member => member.Entity == entities.GetNetEntity(target)).State, Is.EqualTo(MobState.Dead));
            Assert.That(members.All(member => !member.Connected), Is.True);
            Assert.That(system.HasEntrySpace(arena.Comp.Entrance, caster), Is.True);
            var maps = server.System<SharedMapSystem>();
            var outside = maps.CreateMap(out _, runMapInit: true);
            server.System<SharedTransformSystem>().SetCoordinates(target, new EntityCoordinates(outside, Vector2.Zero));
            system.LeaveArena(target, arena.Owner);
            Assert.That(system.GetParty(arena.Owner).Count, Is.EqualTo(7));
            Assert.That(system.HasEntrySpace(arena.Comp.Entrance, outsider), Is.True);
        });
    }

    [TestCase("Beginner")]
    [TestCase("Middle")]
    [TestCase("Senior")]
    public async Task BlackHoleCannotCastOrSpawnInArenaButWorksOnOtherMaps(string tier)
    {
        var (server, arena, caster, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coords = entities.GetComponent<TransformComponent>(caster).Coordinates;
            var action = entities.SpawnEntity("MedievalActionBlackHole" + tier, coords);
            var rules = server.System<SharedBossArenaRulesSystem>();
            Assert.That(rules.CanCast(action, caster, coords), Is.False);
            Assert.That(rules.CanSpawn(new EntProtoId("MedievalProjectileBlackHole" + tier), coords), Is.False);
            var outside = server.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            var outsideCoords = new EntityCoordinates(outside, Vector2.Zero);

            Assert.That(rules.CanCast(action, caster, outsideCoords), Is.False);
            server.System<SharedTransformSystem>().SetCoordinates(caster, outsideCoords);
            Assert.That(rules.CanCast(action, caster, outsideCoords), Is.True);
        });
    }

    [Test]
    public async Task DeferredSupportSpellsAreAbsentWhileNativeLightSpellsRemainBuyable()
    {
        var (server,_,caster,_)=await Setup();
        await server.WaitAssertion(()=>
        {
            var entities=server.EntMan;var prototypes=server.ResolveDependency<IPrototypeManager>();
            foreach(var id in new[]{"MedievalActionRevivificatio","MedievalActionRenovatio",
                "MedievalRevivificatioTouch","MedievalRenovatioTouch","MedievalResurrectionCastEffect"})
                Assert.That(prototypes.HasIndex<EntityPrototype>(id),Is.False,id+" must be outside active resources.");
            foreach(var id in new[]{"MedievalSpellRevivificatio","MedievalSpellRenovatio"})
                Assert.That(prototypes.HasIndex<ImperialListingPrototype>(id),Is.False);
            Assert.That(typeof(Content.Server.Entry.EntryPoint).Assembly.GetType(
                "Content.Server.Imperial.Medieval.Magic.MedievalSupportSpellSystem"),Is.Null);
            foreach(var type in new[]{"MedievalSupportSpellComponent","MedievalSupportTouchComponent","MedievalRegenerationComponent"})
                Assert.That(typeof(Content.Shared.Entry.EntryPoint).Assembly.GetType(
                    "Content.Shared.Imperial.Medieval.Magic."+type),Is.Null);
            foreach(var id in new[]{"HolyTouchSpellCastEffectBeginner","DivineTouchSpellCastEffectBeginner"})
                Assert.That(prototypes.HasIndex<EntityPrototype>(id),Is.True,"Keep the original Light effects.");
            var resources=server.ResolveDependency<Robust.Shared.ContentPack.IResourceManager>();
            Assert.That(resources.ContentFileExists(new ResPath(
                "/Textures/Imperial/Medieval/Magic/Light/Support/icons.rsi/meta.json")),Is.False);
            var book=entities.SpawnEntity("MedievalSpellBookDebug",entities.GetComponent<TransformComponent>(caster).Coordinates);
            var store=entities.GetComponent<ImperialStoreComponent>(book);
            var shops=server.System<Content.Server.Imperial.ImperialStore.ImperialStoreSystem>();
            shops.UpdateUserInterface(caster,book,store);
            Assert.That(server.System<SharedUserInterfaceSystem>().TryGetUiState<ImperialStoreUpdateState>(
                book,ImperialStoreUiKey.Key,out var state),Is.True);
            Assert.That(state!.Listings.Any(l=>l.ID is "MedievalSpellRevivificatio" or "MedievalSpellRenovatio"),Is.False);
            const string ordinary="MedievalActionHolyTouchBeginner";
            Assert.That(state.Listings.Any(l=>l.ID==ordinary),Is.True);
            var balance=store.Balance["MagicMedievalLight"];
            entities.EventBus.RaiseLocalEvent(book,new ImperialStoreBuyListingMessage(store.Listings.Single(l=>l.ID==ordinary))
            {Actor=caster,Entity=entities.GetNetEntity(book),UiKey=ImperialStoreUiKey.Key});
            Assert.That(server.System<SharedActionsSystem>().GetActions(caster).Any(a=>
                entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID==ordinary),Is.True);
            Assert.That(store.Balance["MagicMedievalLight"],Is.EqualTo(balance-FixedPoint2.New(15)));
        });
    }

    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid Caster, EntityUid Target)> Setup()
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
        EntityUid caster = default, target = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
            arena = new(map!.Value.Owner, server.EntMan.GetComponent<BossArenaMapComponent>(map.Value.Owner));
            server.System<SharedMapSystem>().SetPaused(map.Value.Comp.MapId, false);

            caster = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 3.5f, 0.5f));
            target = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 0.5f));
            foreach (var uid in new[] { caster, target })
            {
                server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(uid);
                server.EntMan.RemoveComponent<Content.Shared.Imperial.Medieval.Additions.ShieldOnStartupComponent>(uid);
                server.EntMan.RemoveComponent<RespiratorComponent>(uid);
                server.EntMan.RemoveComponent<BloodstreamComponent>(uid);
            }
            var mana = server.EntMan.EnsureComponent<ManaComponent>(caster);
            mana.MaxMana = 100;
            mana.Mana = 100;
            mana.Regen = 0;
        });
        return (server, arena, caster, target);
    }

}
