using System.Linq;
using System.Numerics;
using System.Text.Json;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task ExecutionLineIsTheLiveRevenantBindingAndLoadsApprovedAnimationStates()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var settings = entities.GetComponent<BossTelegraphSettingsComponent>(boss);
            Assert.That(settings.ExecutionLine, Is.True);
            var style = server.ResolveDependency<IPrototypeManager>().Index(settings.Style);
            Assert.That(style.ExecutionEyesTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/execution_eyes.rsi"));
            Assert.That(style.ExecutionWarningTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/execution_warning.rsi"));
            Assert.That(style.ExecutionTipTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/execution_tip.rsi"));
            Assert.That(style.ExecutionTrailTexture?.ToString(), Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/execution_trail.rsi"));
            Assert.That(settings.ExecutionLineWidth, Is.EqualTo(3));
            var resources = server.ResolveDependency<IResourceManager>();
            foreach (var (path, width, height, states) in new[]
            {
                (style.ExecutionEyesTexture!.Value, 64, 64, new[] { ("eyes", 1, .96), ("shadow", 4, .96) }),
                (style.ExecutionWarningTexture!.Value, 256, 96, new[] { ("warning", 4, 1.28) }),
                (style.ExecutionTipTexture!.Value, 128, 128, new[] { ("strike", 30, 1.2) }),
                (style.ExecutionTrailTexture!.Value, 256, 96, new[] { ("strike", 30, 1.2) }),
            })
            {
                using var meta = JsonDocument.Parse(resources.ContentFileReadAllText(path / "meta.json"));
                Assert.That(meta.RootElement.GetProperty("size").GetProperty("x").GetInt32(), Is.EqualTo(width));
                Assert.That(meta.RootElement.GetProperty("size").GetProperty("y").GetInt32(), Is.EqualTo(height));
                foreach (var (name, count, duration) in states)
                {
                    var state = meta.RootElement.GetProperty("states").EnumerateArray().Single(value => value.GetProperty("name").GetString() == name);
                    var delays = state.GetProperty("delays")[0].EnumerateArray().Select(value => value.GetDouble()).ToArray();
                    Assert.That(delays, Has.Length.EqualTo(count));
                    Assert.That(delays.Sum(), Is.EqualTo(duration).Within(.000001));
                    Assert.That(resources.ContentFileExists(path / (name + ".png")), Is.True);
                }
            }
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss, AnimationBuster(), new[] { players[0] }), Is.True);
            var cast = entities.GetComponent<BossCastingComponent>(boss);
            Assert.That(cast.ExecutionLine, Is.Not.Null);
            Assert.That(cast.TargetMarker, Is.Null);
        });
    }

    [Test]
    public async Task ExecutionLineTracksFreeAngleHitsContinuousStripAndFreezesOnOneImpact()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var bosses = server.System<BossSystem>();
            var lines = server.System<BossExecutionLineSystem>();
            var transforms = server.System<SharedTransformSystem>();
            entities.GetComponent<BossTelegraphSettingsComponent>(boss).ExecutionLine = true;
            transforms.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, .5f));
            bosses.StartBossfight(players.ToList(), boss);
            Assert.That(bosses.TryStartBossCast(boss, AnimationBuster(), new[] { players[0] }), Is.True);
            var cast = entities.GetComponent<BossCastingComponent>(boss);
            Assert.That(cast.TargetMarker, Is.Null);
            var root = cast.ExecutionLine!.Value;
            var line = entities.GetComponent<BossExecutionLineComponent>(root);
            Assert.That(line.Direction, Is.EqualTo(Vector2.UnitX));
            Assert.That(line.Width, Is.EqualTo(3));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null,
                "Execution's strip must not create a tile-pattern warning.");
            var origin = new Vector2(.5f);
            var direction = Vector2.Normalize(new Vector2(3, 2));
            var perpendicular = new Vector2(-direction.Y, direction.X);
            transforms.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, origin + new Vector2(3, 2)));
            var inside = origin + direction * 2 + perpendicular * 1.49f;
            var outside = origin + direction * 2 + perpendicular * 1.51f;
            transforms.SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, inside));
            transforms.SetCoordinates(players[2], new EntityCoordinates(arena.Comp.CombatGrid, outside));
            transforms.SetCoordinates(players[3], new EntityCoordinates(arena.Comp.CombatGrid, origin - direction * 2));
            lines.Update(0);
            Assert.That(Vector2.Distance(line.Direction, direction), Is.LessThan(.00001));
            Assert.That(line.Direction.X, Is.Not.EqualTo(line.Direction.Y), "Aim must retain non-cardinal, non-diagonal angles.");
            Assert.That(new Vector2i((int)MathF.Floor(inside.X), (int)MathF.Floor(inside.Y)),
                Is.EqualTo(new Vector2i((int)MathF.Floor(outside.X), (int)MathF.Floor(outside.Y))),
                "These players share a tile but fall on different sides of the continuous strip.");
            foreach (var player in players) Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            entities.GetComponent<BossCastBarComponent>(boss).EndTime = now;
            bosses.Update(0);
            Assert.That(line.Resolved, Is.True);
            Assert.That(line.ImpactTime, Is.EqualTo(now));
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            Assert.That(entities.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.EqualTo(1));
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.Zero);
            Assert.That(lines.Resolve(boss, players[0], Hit(1)), Is.False, "A strike must not resolve twice.");
            transforms.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, -3.5f, 3.5f));
            lines.Update(0);
            Assert.That(Vector2.Distance(line.Direction, direction), Is.LessThan(.00001), "The cut remains at the actual hit angle.");
            Assert.That(entities.AllEntities<AudioComponent>().Count(audio =>
            {
                var file = audio.Comp.FileName;
                return file.EndsWith("/RevenantBoss/execution_fall.ogg");
            }), Is.EqualTo(1));
            line.EndTime = now;
            lines.Update(0);
            Assert.That(entities.IsQueuedForDeletion(root), Is.True);
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task ExecutionLinePreservesNativeParryForMarkedTankAndHitsOtherBodiesInStrip(bool parry)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            entities.GetComponent<BossTelegraphSettingsComponent>(boss).ExecutionLine = true;
            var transform = server.System<SharedTransformSystem>();
            transform.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, .5f));
            transform.SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, .5f));
            transform.SetCoordinates(players[2], new EntityCoordinates(arena.Comp.CombatGrid, 2.5f, 2.1f));
            transform.SetCoordinates(players[3], new EntityCoordinates(arena.Comp.CombatGrid, -2.5f, .5f));
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var sword = entities.SpawnEntity("MedievalShortSword", entities.GetComponent<TransformComponent>(players[0]).Coordinates);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(players[0], sword), Is.True);
            entities.EnsureComponent<MeleeParryAbleComponent>(players[0]);
            entities.EnsureComponent<MeleeParryStorageComponent>(players[0]);
            var weapon = entities.GetComponent<MeleeParryComponent>(sword);
            weapon.ParriedTime = parry ? server.ResolveDependency<IGameTiming>().CurTime : TimeSpan.Zero;
            var prototype = server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>("RevenantBoss");
            var damage = new DamageSpecifier(((BossEncounterComponent)prototype.Components["BossEncounter"].Component).Damage["buster"]);
            var attack = new BossTankBusterAttack { CastTime = 5, CastName = "Revenant_Boss_Execution",
                Actions = new() { new BossTankBusterImpact { Damage = damage } } };
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss, attack, new[] { players[0] }), Is.True);
            entities.GetComponent<BossCastBarComponent>(boss).EndTime = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), parry ? Is.Zero : Is.GreaterThanOrEqualTo(500));
            if (parry) Assert.That(weapon.LastParryTime, Is.EqualTo(server.ResolveDependency<IGameTiming>().CurTime));
            Assert.That(entities.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.GreaterThanOrEqualTo(500));
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(players[3]).TotalDamage.Float(), Is.Zero);
        });
    }

    [TestCase("cancel")] [TestCase("target-leaves")] [TestCase("target-dies")] [TestCase("target-deletion")]
    [TestCase("victory")] [TestCase("wipe")] [TestCase("boss-deletion")] [TestCase("map-deletion")]
    public async Task ExecutionLineClearsEyesAndWarningWithoutDamageAfterInterruption(string reason)
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            entities.GetComponent<BossTelegraphSettingsComponent>(boss).ExecutionLine = true;
            var bosses = server.System<BossSystem>();
            bosses.StartBossfight(players.ToList(), boss);
            bosses.TryStartBossCast(boss, AnimationBuster(), new[] { players[0] });
            var effect = entities.GetComponent<BossCastingComponent>(boss).ExecutionLine!.Value;
            switch (reason)
            {
                case "cancel": bosses.CancelBossCast(boss); break;
                case "target-leaves": server.System<SharedTransformSystem>().SetCoordinates(players[0], entities.GetComponent<TransformComponent>(gate).Coordinates); break;
                case "target-dies": server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Dead); break;
                case "target-deletion": entities.DeleteEntity(players[0]); break;
                case "victory": bosses.DamageBoss(boss, 100000); break;
                case "wipe": foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead); break;
                case "boss-deletion": entities.DeleteEntity(boss); break;
                case "map-deletion": entities.DeleteEntity(arena.Owner); break;
            }
            if (entities.EntityExists(boss) && entities.TryGetComponent<BossCastBarComponent>(boss, out var bar))
            {
                bar.EndTime = server.ResolveDependency<IGameTiming>().CurTime;
                bosses.Update(0);
            }
            server.System<BossExecutionLineSystem>().Update(0);
            Assert.That(!entities.EntityExists(effect) || entities.IsQueuedForDeletion(effect), Is.True);
            foreach (var player in players.Where(entities.EntityExists))
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            Assert.That(entities.AllEntities<AudioComponent>().Any(audio =>
            {
                var file = audio.Comp.FileName;
                return file.EndsWith("/RevenantBoss/execution_fall.ogg");
            }), Is.False);
        });
    }

    [Test]
    public async Task ExecutionLineAndCastPauseTogether()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid root = default;
        TimeSpan start = default, impact = default, end = default;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.EntMan.GetComponent<BossTelegraphSettingsComponent>(boss).ExecutionLine = true;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.System<BossSystem>().TryStartBossCast(boss, AnimationBuster(), new[] { players[0] });
            root = server.EntMan.GetComponent<BossCastingComponent>(boss).ExecutionLine!.Value;
            var line = server.EntMan.GetComponent<BossExecutionLineComponent>(root);
            start = line.StartTime; impact = line.ImpactTime; end = line.EndTime;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var line = server.EntMan.GetComponent<BossExecutionLineComponent>(root);
            Assert.That(line.Resolved, Is.False);
            Assert.That(line.ImpactTime, Is.EqualTo(impact));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var shift = line.ImpactTime - impact;
            Assert.That(shift.TotalSeconds, Is.GreaterThan(1));
            Assert.That(line.StartTime - start, Is.EqualTo(shift));
            Assert.That(line.EndTime - end, Is.EqualTo(shift));
            Assert.That(line.ImpactTime, Is.EqualTo(server.EntMan.GetComponent<BossCastBarComponent>(arena.Comp.Boss).EndTime));
        });
    }
}

[TestFixture]
public sealed class ExecutionLineGeometryTest
{
    [TestCase(0f)] [TestCase(.37f)] [TestCase(1.1f)] [TestCase(2.8f)] [TestCase(-2.3f)]
    [TestCase(MathF.PI / 4)] [TestCase(MathF.PI / 2)] [TestCase(3 * MathF.PI / 4)]
    [TestCase(MathF.PI)] [TestCase(-3 * MathF.PI / 4)] [TestCase(-MathF.PI / 2)] [TestCase(-MathF.PI / 4)]
    public void ContinuousStripKeepsItsWidthAndReachesArenaEdgeAtAnyAngle(float angle)
    {
        var origin = new Vector2(.5f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var normal = new Vector2(-direction.Y, direction.X);
        var length = BossExecutionLineVisuals.LengthToEdge(origin, direction, 5);
        Assert.That(BossExecutionLineVisuals.Contains(origin + direction * 3 + normal * 1.49f, origin, direction, length, 3), Is.True);
        Assert.That(BossExecutionLineVisuals.Contains(origin + direction * 3 + normal * 1.51f, origin, direction, length, 3), Is.False);
        Assert.That(BossExecutionLineVisuals.Contains(origin - direction, origin, direction, length, 3), Is.False);
        Assert.That(BossExecutionLineVisuals.Contains(origin + direction * (length + .01f), origin, direction, length, 3), Is.False);
        var edge = origin + direction * (length - BossExecutionLineVisuals.EdgeExtension);
        Assert.That(MathF.Max(MathF.Abs(edge.X - .5f), MathF.Abs(edge.Y - .5f)), Is.EqualTo(5.5f).Within(.00001));
        Assert.That(Vector2.Distance(BossExecutionLineVisuals.Aim(origin, origin, direction), direction), Is.LessThan(.00001));
    }
}
