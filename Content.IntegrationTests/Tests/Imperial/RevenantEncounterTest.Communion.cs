using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(true, false)] [TestCase(false, true)]
    public async Task SentencingStackShowsLanternWithoutFogOrArenaDimming(bool preload, bool storedStrike)
    {
        var (server, arena, players, _) = await SetupCommunion();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Stack, CastTime = 4, Damage = Hit(1) },
                preloadOnly: preload, storedStrike: storedStrike, selectedTargets: [players[1]]), Is.True);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value;
            var now = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(1);
            Assert.That(warning.Comp.StackTarget, Is.EqualTo(players[1]));
            Assert.That(BossCommunionVisuals.FogOpacity(warning.Comp, now), Is.Zero);
            Assert.That(BossCommunionVisuals.BossLightFactor(warning.Comp, now), Is.EqualTo(1));
            Assert.That(BossCommunionVisuals.LightEnergy(warning.Comp, now), Is.Zero);
            server.System<BossCommunionLightSystem>().Update(0);
            Assert.That(server.System<BossCommunionLightSystem>().FactorFor(boss), Is.EqualTo(1));
            Assert.That(server.System<SharedPointLightSystem>().TryGetLight(warning.Owner, out _), Is.False);
            warning.Comp.Preload = false;
            warning.Comp.Impact = true;
            warning.Comp.StartTime = now;
            Assert.That(BossCommunionVisuals.FogOpacity(warning.Comp, now), Is.EqualTo(.52f));
        });
    }
    [Test]
    public async Task CommunionFogShaderParsesInTheGameEngine()
    {
        var (server, _, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            using var reader = resources.ContentFileReadText(new ResPath("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/communion_fog.swsl"));
            var parser = typeof(Robust.Client.Graphics.ShaderPrototype).Assembly.GetType("Robust.Client.Graphics.ShaderParser", true)!;
            var parse = parser.GetMethod("Parse", [typeof(string), typeof(IResourceManager)])!;
            Assert.That(parse.Invoke(null, [reader.ReadToEnd(), resources]), Is.Not.Null);
        });
    }

    [TestCase(0, 0)] [TestCase(5, 5)] [TestCase(-5, -5)]
    public async Task CommunionTracksAndFreezesTheActualRefugeWithoutChangingDamage(int x, int y)
    {
        var (server, arena, players, _) = await SetupCommunion();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            var visuals = server.System<BossTelegraphSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Stack, CastTime = 4, Damage = Hit(1) }, selectedTargets: [players[1]]), Is.True);
            var center = new Vector2i(x, y);
            var expected = BossTilePatterns.Danger(BossTelegraphPattern.Stack, [center]);
            var unsafeTile = expected.First();
            var transform = server.System<SharedTransformSystem>();
            foreach (var player in players)
                transform.SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, x + .5f, y + .5f));
            transform.SetCoordinates(players[2], new EntityCoordinates(arena.Comp.CombatGrid, unsafeTile.X + .5f, unsafeTile.Y + .5f));
            Assert.That(visuals.SnapshotDanger(boss), Is.EquivalentTo(expected));
            var warning = visuals.GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.StackCenter, Is.EqualTo(center));
            Assert.That(warning.StackTarget, Is.EqualTo(players[1]));
            var clock = warning.AnimationStartTime;
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            Assert.That(effect.Comp.StackCenter, Is.EqualTo(center));
            Assert.That(effect.Comp.StackTarget, Is.EqualTo(players[1]));
            Assert.That(effect.Comp.AnimationStartTime, Is.EqualTo(clock));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(expected));
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds, Is.EqualTo(2.2).Within(.000001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime, Is.EqualTo(2.2).Within(.000001));
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.EqualTo(1));
            foreach (var player in new[] { players[0], players[1], players[3] })
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e => e.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            transform.SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, -3.5f, 3.5f));
            visuals.Update(0);
            Assert.That(effect.Comp.StackCenter, Is.EqualTo(center), "The fading refuge must not follow the departing target.");
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CommunionUsesNativeAmberLightDimsOnlyBossAndRestoresOnCancellation()
    {
        var (server, arena, players, _) = await SetupCommunion();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Stack, CastTime = 4, Damage = Hit(1) }, selectedTargets: [players[1]]), Is.True);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value;
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            warning.Comp.StartTime = warning.Comp.AnimationStartTime = now - TimeSpan.FromSeconds(1);
            var lights = server.System<SharedPointLightSystem>();
            var unrelated = lights.EnsureLight(players[3]);
            lights.SetEnergy(players[3], 2, unrelated);
            var native = server.System<BossCommunionLightSystem>();
            native.Update(0);
            server.System<BossLightSystem>().Update(0);
            Assert.That(lights.TryGetLight(warning.Owner, out var lantern), Is.True);
            Assert.That(lantern!.Enabled, Is.True);
            Assert.That(lantern.Color, Is.EqualTo(BossCommunionVisuals.LanternColor));
            Assert.That(lantern.Energy, Is.EqualTo(10));
            Assert.That(lantern.CastShadows, Is.False);
            var refuge = BossCommunionVisuals.SafeBounds(warning.Comp.StackCenter!.Value, warning.Comp.HalfSize);
            Assert.That(lantern.Offset + new Vector2(.5f), Is.EqualTo(refuge.Center));
            Assert.That(lantern.Radius, Is.EqualTo(BossCommunionVisuals.NativeLightRadius(refuge)));
            Assert.That(lantern.Radius, Is.EqualTo(2.5));
            Assert.That(native.FactorFor(boss), Is.EqualTo(.12).Within(.000001));
            Assert.That(lights.TryGetLight(boss, out var bossLight), Is.True);
            Assert.That(bossLight!.Energy, Is.EqualTo(.36).Within(.000001));
            Assert.That(unrelated.Energy, Is.EqualTo(2));
            mechanics.Cancel(boss);
            native.Update(0);
            server.System<BossLightSystem>().Update(0);
            Assert.That(server.EntMan.EntityExists(warning.Owner), Is.False);
            Assert.That(native.FactorFor(boss), Is.EqualTo(1));
            Assert.That(bossLight.Energy, Is.EqualTo(3));
            Assert.That(unrelated.Energy, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CommunionNativeLightFadesWithFogAndRestoresAfterImpactCleanup()
    {
        var (server, arena, players, _) = await SetupCommunion();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Stack, CastTime = 4, Damage = Hit(1) }, selectedTargets: [players[1]]), Is.True);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = now;
            mechanics.Update(0);
            var effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact);
            var native = server.System<BossCommunionLightSystem>();
            var lights = server.System<SharedPointLightSystem>();
            native.Update(0);
            Assert.That(lights.TryGetLight(effect.Owner, out var lantern), Is.True);
            Assert.That(lantern!.Energy, Is.EqualTo(10));
            effect.Comp.StartTime = now - TimeSpan.FromSeconds(1.6);
            native.Update(0);
            server.System<BossLightSystem>().Update(0);
            Assert.That(lantern.Energy, Is.EqualTo(5).Within(.000001));
            Assert.That(native.FactorFor(boss), Is.EqualTo(.56).Within(.000001));
            Assert.That(BossCommunionVisuals.FogOpacity(effect.Comp, now), Is.EqualTo(.26).Within(.000001));
            server.EntMan.DeleteEntity(effect.Owner);
            native.Update(0);
            server.System<BossLightSystem>().Update(0);
            Assert.That(native.FactorFor(boss), Is.EqualTo(1));
            Assert.That(lights.TryGetLight(boss, out var restored), Is.True);
            Assert.That(restored!.Energy, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task CommunionLightFitsAllRefugePlacementsAndFogFeathersBeyondSafeDamageTiles()
    {
        var (server, _, _, _) = await SetupCommunion();
        await server.WaitAssertion(() =>
        {
            var style = server.ResolveDependency<IPrototypeManager>().Index<BossTelegraphStylePrototype>("RevenantBossTelegraphStyle");
            Assert.That(BossCommunionVisuals.Enabled(style, BossTelegraphPattern.Stack), Is.True);
            Assert.That(BossCommunionVisuals.ReplacesLegacyCue(style, BossActorMarkerKind.Stack), Is.True);
            Assert.That(BossCommunionVisuals.ReplacesLegacyCue(style, BossActorMarkerKind.Tank), Is.False);
            foreach (var center in BossTilePatterns.Platform)
            {
                var refuge = BossCommunionVisuals.SafeBounds(center, 5);
                var safe = BossTilePatterns.Platform.Except(BossTilePatterns.Danger(BossTelegraphPattern.Stack, [center]));
                foreach (var tile in safe)
                    Assert.That(BossCommunionVisuals.FogMask(new Vector2(tile.X + .5f, tile.Y + .5f), refuge), Is.Zero);
                var radius = BossCommunionVisuals.NativeLightRadius(refuge);
                Assert.That(radius, Is.EqualTo(2.5));
                Assert.That(BossCommunionVisuals.LightMask(refuge.Center, refuge), Is.EqualTo(1));
                Assert.That(BossCommunionVisuals.LightMask(new Vector2(refuge.Right, refuge.Center.Y), refuge), Is.Zero);
                Assert.That(BossCommunionVisuals.LightMask(new Vector2(refuge.Right + .1f, refuge.Center.Y), refuge), Is.Zero);
                var edge = new Vector2(refuge.Right, refuge.Center.Y);
                Assert.That(BossCommunionVisuals.FogMask(edge, refuge), Is.Zero);
                Assert.That(BossCommunionVisuals.FogMask(edge + new Vector2(.3f, 0), refuge), Is.EqualTo(.5).Within(.000001));
                Assert.That(BossCommunionVisuals.FogMask(edge + new Vector2(.6f, 0), refuge), Is.EqualTo(1).Within(.000001));
                foreach (var patch in BossCommunionVisuals.EdgePatches(refuge))
                {
                    var overlapX = Math.Min(patch.Bounds.Right, refuge.Right) - Math.Max(patch.Bounds.Left, refuge.Left);
                    var overlapY = Math.Min(patch.Bounds.Top, refuge.Top) - Math.Max(patch.Bounds.Bottom, refuge.Bottom);
                    Assert.That(overlapX <= 0 || overlapY <= 0, Is.True, "Impact edge must leave the refuge clear.");
                    Assert.That(patch.Bounds.Width * 16, Is.EqualTo(patch.Region.Width));
                    Assert.That(patch.Bounds.Height * 16, Is.EqualTo(patch.Region.Height));
                }
            }
        });
    }

    [Test]
    public async Task CommunionWarningFogIsSubtleAndOriginalAnimationClockSurvivesPausing()
    {
        var (server, arena, players, _) = await SetupCommunion();
        EntityUid root = default;
        TimeSpan clock = default, start = default;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Stack, CastTime = 4, Damage = Hit(1) }, selectedTargets: [players[1]]), Is.True);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value;
            root = warning.Owner; clock = warning.Comp.AnimationStartTime; start = warning.Comp.StartTime;
            Assert.That(clock, Is.EqualTo(start));
            Assert.That(BossCommunionVisuals.FogOpacity(warning.Comp, clock + TimeSpan.FromSeconds(.1)), Is.Zero);
            Assert.That(BossCommunionVisuals.FogOpacity(warning.Comp, clock + TimeSpan.FromSeconds(1)), Is.EqualTo(.075).Within(.000001));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossTelegraphComponent>(root).AnimationStartTime, Is.EqualTo(clock));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var warning = server.EntMan.GetComponent<BossTelegraphComponent>(root);
            var shift = warning.StartTime - start;
            Assert.That(shift.TotalSeconds, Is.GreaterThan(1));
            Assert.That(warning.AnimationStartTime - clock, Is.EqualTo(shift));
        });
    }

    private async Task<(RobustIntegrationTest.ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena,
        EntityUid[] Players, EntityUid Gate)> SetupCommunion()
    {
        var setup = await Setup();
        await setup.Server.WaitAssertion(() =>
        {

            var style = setup.Server.ResolveDependency<IPrototypeManager>().Index<BossTelegraphStylePrototype>("RevenantBossTelegraphStyle");
            Assert.That(style.CommunionFogTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/communion_fog.rsi"));
            Assert.That(style.CommunionLanternTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/communion_lantern.rsi"));
            Assert.That(style.CommunionEdgeTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/communion_floor.rsi"));
            Assert.That(style.StackTexture, Is.Null);
            Assert.That(style.StackHighlightColor.A, Is.Zero);
        });
        return setup;
    }
}
