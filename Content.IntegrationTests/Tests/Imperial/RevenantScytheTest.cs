using static Content.IntegrationTests.Tests.Imperial.MeleeSwingTestData;
using System.IO;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.ChargedAttack;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Localization;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantScytheTest : RevenantIntegrationTestBase
{
    [TestCase(-1, 1)]
    [TestCase(0, 1)]
    [TestCase(2, 1.5)]
    [TestCase(4, 2)]
    [TestCase(40, 2)]
    [TestCase(60.5, 2)]
    public void ChargeUsesTotalSecondsAndCapsAtTwiceTheSize(double seconds, double expected)
    {
        var ratio = MeleeSwingVisuals.ChargeRatio(TimeSpan.FromSeconds(seconds), 4);
        Assert.That(MeleeSwingVisuals.SwingScale(ratio, 2), Is.EqualTo(expected).Within(0.0001));
    }

    [Test]
    public void BossSlashPointsAlongTheAimInEveryDirectionAndOnRotatedGrids()
    {
        foreach (var gridDegrees in new[] { 0, 45, 90, 180 })
        foreach (var aimDegrees in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            var grid = Angle.FromDegrees(gridDegrees);
            var localAim = Angle.FromDegrees(aimDegrees).ToVec() * 3;
            var expected = grid.RotateVec(localAim).Normalized();
            var actual = MeleeSwingVisuals.SwingTextureRotation(localAim, grid).ToVec();
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.0001));
        }
    }

    [TestCase(0, 2.6f, false)]
    [TestCase(0, 1f, true)]
    [TestCase(2, 2.1f, true)]
    [TestCase(4, 2.6f, true)]
    [TestCase(40, 3.6f, false)]
    public async Task ChargedCleaveHitsAtItsActualReachAndDoesNotHitBehind(
        double chargeSeconds, float targetDistance, bool shouldHit)
    {
        var (server, user, weapon, coordinates) = await Setup();
        EntityUid target = default, behind = default;
        await server.WaitAssertion(() =>
        {
            target = server.EntMan.SpawnEntity("MobHuman",
                new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(targetDistance, 0)));
            behind = server.EntMan.SpawnEntity("MobHuman",
                new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(-2, 0)));

            server.EntMan.RemoveComponent<ShieldOnStartupComponent>(target);
            server.EntMan.RemoveComponent<ShieldOnStartupComponent>(behind);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            var originalRange = melee.Range;
            var originalAngle = melee.Angle;
            var originalAnimation = melee.WideAnimation;
            var charged = entities.GetComponent<ChargedAttackComponent>(weapon);
            charged.CurrentAttacking = true;
            charged.AttackStart = server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(chargeSeconds);
            melee.NextAttack = TimeSpan.Zero;

            var aim = new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(.1f, 0));
            Assert.That(server.System<MeleeSwingSystem>().TryRelease(weapon, user, aim), Is.True);
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float() > 0, Is.EqualTo(shouldHit));
            Assert.That(entities.GetComponent<DamageableComponent>(behind).TotalDamage.Float(), Is.Zero);
            Assert.That(melee.Range, Is.EqualTo(originalRange), "Charging must not permanently change melee reach.");
            Assert.That(melee.Angle, Is.EqualTo(originalAngle));
            Assert.That(melee.WideAnimation, Is.EqualTo(originalAnimation));
        });
    }

    [Test]
    public async Task OnlyConfirmedSwipesAlternateAndEachScytheKeepsItsOwnDirection()
    {
        var (server, user, weapon, coordinates) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var scythes = server.System<MeleeSwingSystem>();
            var scythe = entities.GetComponent<MeleeSwingComponent>(weapon);
            var charged = entities.GetComponent<ChargedAttackComponent>(weapon);
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            var aim = new EntityCoordinates(coordinates.EntityId, coordinates.Position + Vector2.UnitX);
            var other = entities.SpawnEntity("RevenantBossScythe", coordinates);
            charged.StaminaDamage = 0;
            charged.CurrentAttacking = true;
            charged.AttackStart = now;
            melee.NextAttack = TimeSpan.Zero;
            Assert.That(scythe.NextSwingLeftToRight, Is.True);
            Assert.That(scythes.TryRelease(weapon, user, aim), Is.True);
            Assert.That(scythe.NextSwingLeftToRight, Is.False);
            Assert.That(scythes.TryRelease(weapon, user, aim), Is.False, "Cooldown prevents another confirmed swipe.");
            Assert.That(scythe.NextSwingLeftToRight, Is.False, "A blocked swipe keeps the next direction.");
            Assert.That(scythes.TryRelease(weapon, user, coordinates), Is.False, "Zero-length aim must be rejected.");
            Assert.That(scythe.NextSwingLeftToRight, Is.False);
            melee.NextAttack = TimeSpan.Zero;
            Assert.That(scythes.TryRelease(weapon, user, aim), Is.True);
            Assert.That(scythe.NextSwingLeftToRight, Is.True);
            Assert.That(entities.GetComponent<MeleeSwingComponent>(other).NextSwingLeftToRight, Is.True,
                "Another scythe must not inherit this weapon's attack sequence.");
        });
    }

    [TestCase(.45f, .7f)]
    [TestCase(.225f, .35f)]
    [TestCase(.9f, 1.4f)]
    public void SwipeMotionAndCompleteFadeScaleWithNormalSwingTiming(float normalLength, float duration)
    {
        var scale = MeleeSwingVisuals.SwingPlaybackScale(Style, normalLength);
        Assert.That(Style.Duration * scale, Is.EqualTo(duration / 1.15f).Within(.0001));
        Assert.That(MeleeSwingVisuals.SwingFrame(Style, .69f * scale, normalLength), Is.EqualTo(34),
            "Playback must reach the approved fully transparent frame before removal.");
        Assert.That(MeleeSwingVisuals.SwingFrame(Style, 10, normalLength), Is.EqualTo(34), "Never wrap the one-shot swipe.");
        Assert.That(MeleeSwingVisuals.IsSwingAnimation(MeleeSwingVisuals.SwingMarker(true)), Is.True);
        Assert.That(MeleeSwingVisuals.IsSwingAnimation(MeleeSwingVisuals.SwingMarker(false)), Is.True);
        Assert.That(MeleeSwingVisuals.SwingState(Style, true), Is.EqualTo("left-to-right"));
        Assert.That(MeleeSwingVisuals.SwingState(Style, false), Is.EqualTo("right-to-left"));
    }

    [Test]
    public async Task UnwieldingDroppingAndAnotherHolderCannotReleaseTheCharge()
    {
        var (server, user, weapon, coordinates) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var scythes = server.System<MeleeSwingSystem>();
            var charged = entities.GetComponent<ChargedAttackComponent>(weapon);
            var wieldable = entities.GetComponent<WieldableComponent>(weapon);
            var aim = new EntityCoordinates(coordinates.EntityId, coordinates.Position + Vector2.UnitX);
            var stranger = entities.SpawnEntity("MobHuman", coordinates);
            Assert.That(scythes.CanCharge(weapon, stranger), Is.False);
            Assert.That(scythes.TryRelease(weapon, stranger, aim), Is.False);
            charged.CurrentAttacking = true;
            charged.AttackStart = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(server.System<SharedWieldableSystem>().TryUnwield(weapon, wieldable, user), Is.True);
            Assert.That(charged.CurrentAttacking, Is.False);
            Assert.That(scythes.TryRelease(weapon, user, aim), Is.False);
        });

        await server.WaitRunTicks(75);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var scythes = server.System<MeleeSwingSystem>();
            var charged = entities.GetComponent<ChargedAttackComponent>(weapon);
            var wieldable = entities.GetComponent<WieldableComponent>(weapon);
            var aim = new EntityCoordinates(coordinates.EntityId, coordinates.Position + Vector2.UnitX);
            Assert.That(server.System<SharedWieldableSystem>().TryWield(weapon, wieldable, user), Is.True);
            charged.CurrentAttacking = true;
            charged.AttackStart = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(server.System<SharedHandsSystem>().TryDrop(user, weapon), Is.True);
            Assert.That(charged.CurrentAttacking, Is.False);
            Assert.That(scythes.TryRelease(weapon, user, aim), Is.False);
        });
    }

    [Test]
    public async Task DamageDoublesOnlyTheBossScytheInBothWieldStates()
    {
        var (server, user, weapon, coordinates) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var melee = server.System<SharedMeleeWeaponSystem>();
            var regular = entities.SpawnEntity("MedievalScythe", coordinates);
            var wielded = melee.GetDamage(weapon, user).DamageDict["Slash"].Float();
            Assert.That(wielded, Is.EqualTo(32));
            var wieldable = entities.GetComponent<WieldableComponent>(weapon);
            server.System<SharedWieldableSystem>().TryUnwield(weapon, wieldable, user);
            var single = melee.GetDamage(weapon, user);
            var ordinary = melee.GetDamage(regular, user);
            Assert.That(single.DamageDict["Slash"].Float(), Is.EqualTo(16));
            Assert.That(single.DamageDict["Slash"], Is.EqualTo(ordinary.DamageDict["Slash"] * 2));
            Assert.That(single.DamageDict["ParryAble"], Is.EqualTo(ordinary.DamageDict["ParryAble"]));
        });
    }

    [Test]
    public async Task AnotherWeaponUsesItsOwnSwingStyleWithoutRevenantSettings()
    {
        var (server, user, first, coordinates) = await Setup();
        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var changed = new Dictionary<Type, HashSet<string>>();
            prototypes.LoadString("""
                - type: meleeSwingStyle
                  id: IndependentSwingTestStyle
                  texture: /Textures/Effects/portal.rsi
                  leftState: portal-artifact
                  rightState: portal-artifact
                  frameCount: 1
                  frameSeconds: 0.2
                  referenceSwingSeconds: 0.4
                  playbackSpeed: 2
                  arc: 90
                - type: entity
                  id: IndependentSwingTestWeapon
                  parent: MedievalScythe
                  components:
                  - type: MeleeSwing
                    style: IndependentSwingTestStyle
                    maximumSwingScale: 3
                  - type: ChargedAttack
                    maxAttackTime: 4
                """, changed: changed);
            prototypes.ReloadPrototypes(changed);
            var entities = server.EntMan;
            Assert.That(server.System<SharedHandsSystem>().TryDrop(user, first), Is.True);
            var weapon = entities.SpawnEntity("IndependentSwingTestWeapon", coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, weapon), Is.True);
            var swings = server.System<MeleeSwingSystem>();
            Assert.That(swings.CanCharge(weapon, user), Is.True, "Wielding is optional per weapon.");
            var component = entities.GetComponent<MeleeSwingComponent>(weapon);
            var style = prototypes.Index(component.Style);
            Assert.That(style.Arc.Degrees, Is.EqualTo(90).Within(0.001));
            Assert.That(MeleeSwingVisuals.SwingScale(1, component.MaximumSwingScale), Is.EqualTo(3));
            Assert.That(MeleeSwingVisuals.SwingPlaybackScale(style, 0.4f), Is.EqualTo(0.5f));
            var target = entities.SpawnEntity("MobHuman", new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(2.6f, 0)));
            entities.RemoveComponent<ShieldOnStartupComponent>(target);
            var charged = entities.GetComponent<ChargedAttackComponent>(weapon);
            charged.StaminaDamage = 0;
            charged.CurrentAttacking = true;
            charged.AttackStart = server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(4);
            var melee = entities.GetComponent<MeleeWeaponComponent>(weapon);
            melee.NextAttack = TimeSpan.Zero;
            var angle = melee.Angle;
            Assert.That(swings.TryRelease(weapon, user, new EntityCoordinates(coordinates.EntityId, coordinates.Position + Vector2.UnitX)), Is.True);
            Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0));
            Assert.That(melee.Angle, Is.EqualTo(angle), "A style must not permanently replace the weapon's native attack settings.");
            Assert.That(entities.GetComponent<MeleeSwingComponent>(first).Style, Is.Not.EqualTo(component.Style));
        });
    }

    private async Task<(ServerIntegrationInstance Server, EntityUid User, EntityUid Weapon, EntityCoordinates Coordinates)> Setup()
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
        EntityUid user = default, weapon = default;
        EntityCoordinates coordinates = default;
        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId, runMapInit: true);
            var grid = server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            for (var x = -5; x <= 5; x++)
            for (var y = -5; y <= 5; y++)
                maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
            coordinates = new EntityCoordinates(grid.Owner, .5f, .5f);
            user = server.EntMan.SpawnEntity("MobHuman", coordinates);
            server.EntMan.RemoveComponent<ShieldOnStartupComponent>(user);
            weapon = server.EntMan.SpawnEntity("RevenantBossScythe", coordinates);
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(weapon).EntityName,
                Is.EqualTo(server.ResolveDependency<ILocalizationManager>().GetString("Revenant_Boss_Scythe")));
            var item = server.EntMan.GetComponent<ItemComponent>(weapon);
            Assert.That(item.InhandVisuals[HandLocation.Left].Select(l => l.State),
                Is.EqualTo(new[] { "carry-front-left", "carry-rear-left" }));
            Assert.That(item.InhandVisuals[HandLocation.Right].Select(l => l.State),
                Is.EqualTo(new[] { "carry-front-right", "carry-rear-right" }));
            Assert.That(item.InhandVisuals.Values.SelectMany(l => l).All(l => l.Scale == null || l.Scale == Vector2.One),
                Is.True, "Native held artwork must keep its authored scale.");
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, weapon), Is.True);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<SharedWieldableSystem>().CanWield(weapon,
                server.EntMan.GetComponent<WieldableComponent>(weapon), user), Is.True);
            Assert.That(server.System<SharedWieldableSystem>().TryWield(weapon,
                server.EntMan.GetComponent<WieldableComponent>(weapon), user), Is.True);
            server.System<SharedCombatModeSystem>().SetInCombatMode(user, true);
        });
        await server.WaitRunTicks(2);
        return (server, user, weapon, coordinates);
    }
}
