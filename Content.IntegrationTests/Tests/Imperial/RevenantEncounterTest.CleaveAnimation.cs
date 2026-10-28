using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Boss;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Components;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(false)] [TestCase(true)]
    public async Task CleaveAnimationPreviewsResolveSilentlyWithoutStartingCombatOrHurtingPlayers(bool stored)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            if (stored)
            {
                Assert.That(server.System<BossSequenceSystem>().TryStart(boss, new BossPreparedSequenceAttack(),
                    preview: true, queue: [BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum]), Is.True);
                for (var index = 0; index < 2; index++)
                {
                    entities.GetComponent<BossSequenceComponent>(boss).Deadline = now;
                    server.System<BossSequenceSystem>().Update(0);
                    Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entry => entry.Comp.Impact), Is.False);
                }
                entities.GetComponent<BossSequenceComponent>(boss).Deadline = now;
                server.System<BossSequenceSystem>().Update(0);
            }
            else
            {
                Assert.That(server.System<BossSystem>().TryStartBossCast(boss,
                    new BossTargetedAttack { Telegraph = BossTelegraphPattern.Frons, CastTime = 3 }, [boss], false), Is.True);
                entities.GetComponent<BossCastBarComponent>(boss).EndTime = now;
                server.System<BossSystem>().Update(0);
            }
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact);
            Assert.That(effect.Comp.EndTime, Is.GreaterThan(effect.Comp.StartTime));
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.False);
            Assert.That(entities.AllEntities<AudioComponent>().Any(entry =>
            {
                var file = entry.Comp.FileName;
                return file.EndsWith("/RevenantBoss/slash.ogg");
            }), Is.False);
            foreach (var player in players)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
        });
    }

    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    public async Task CleaveAnimationStartsOnlyAtConfirmedHitAndPreservesVictims(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,
                new BossTilePatternAttack { Telegraph = pattern, CastTime = 4, Damage = Hit(1) }), Is.True);
            var visuals = server.System<BossTelegraphSystem>();
            var warning = visuals.GetTelegraph(boss)!.Value.Comp;
            var danger = warning.Danger.ToArray();
            var safe = warning.Safe.First();
            for (var index = 0; index < players.Length; index++)
            {
                var tile = index == 0 ? danger.First() : safe;
                server.System<SharedTransformSystem>().SetCoordinates(players[index],
                    new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            }
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(effect => effect.Comp.Impact), Is.False);
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.Zero);
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
            var style = server.ResolveDependency<IPrototypeManager>().Index(effect.Comp.Style);
            Assert.That(style.CleaveLightTexture?.ToString(),
                Is.EqualTo("/Textures/Imperial/Medieval/Mobs/Bosses/RevenantBoss/cleave_boss_purple.png"));
            Assert.That(style.CleaveLightAnchorPixels, Is.EqualTo(new Vector2(588, 640)));
            Assert.That(style.CleaveLightRadiusPixels, Is.EqualTo(550));
            Assert.That(style.CleaveLightScale, Is.EqualTo(0.7f));
            Assert.That(style.CleaveLightMovementScale, Is.EqualTo(1));
            Assert.That(effect.Comp.MovementCleave, Is.False);
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That(effect.Comp.StartTime, Is.EqualTo(server.ResolveDependency<IGameTiming>().CurTime));
            Assert.That((effect.Comp.EndTime - effect.Comp.StartTime).TotalSeconds,
                Is.EqualTo(BossCleaveVisuals.DurationSeconds).Within(0.00001));
            Assert.That(entities.GetComponent<TimedDespawnComponent>(effect.Owner).Lifetime,
                Is.EqualTo(BossCleaveVisuals.DurationSeconds).Within(0.00001));
            Assert.That(entities.HasComponent<PhysicsComponent>(effect.Owner), Is.False);
            Assert.That(entities.HasComponent<DamageableComponent>(effect.Owner), Is.False);
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(entry => entry.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect"), Is.False);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            foreach (var player in players.Skip(1))
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage.Float(), Is.Zero);
            server.System<BossMechanicSystem>().Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(1));
            Assert.That(visuals.GetTelegraph(boss), Is.Null);
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task CleaveAnimationAlsoUsesStoredAndDisplacedStrikeSnapshots(bool displaced)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var visuals = server.System<BossTelegraphSystem>();
            if (displaced)
            {
                Assert.That(server.System<BossTraversalSystem>().TryStart(boss, BossTraversalKind.StepCleave,
                    Hit(1), direction: 1, distance: 2), Is.True);
                AdvanceTraversal(server, boss);
                AdvanceTraversal(server, boss);
            }
            else
            {
                Assert.That(server.System<BossSequenceSystem>().TryStart(boss,
                    new BossPreparedSequenceAttack { Damage = Hit(1) }, queue: [BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum]), Is.True);
                for (var index = 0; index < 2; index++)
                {
                    entities.GetComponent<BossSequenceComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                    server.System<BossSequenceSystem>().Update(0);
                }
            }
            var warning = visuals.GetTelegraph(boss)!.Value.Comp;
            var pattern = warning.Pattern;
            var origin = warning.PatternOrigin;
            var danger = warning.Danger.ToArray();
            Assert.That(warning.MovementCleave, Is.EqualTo(displaced));
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entry => entry.Comp.Impact), Is.False);
            if (displaced) AdvanceTraversal(server, boss);
            else
            {
                entities.GetComponent<BossSequenceComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossSequenceSystem>().Update(0);
            }
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact);
            Assert.That(effect.Comp.Pattern, Is.EqualTo(pattern));
            Assert.That(effect.Comp.PatternOrigin, Is.EqualTo(origin));
            Assert.That(effect.Comp.MovementCleave, Is.EqualTo(displaced));
            Assert.That(effect.Comp.Danger, Is.EquivalentTo(danger));
            Assert.That(effect.Comp.EndTime, Is.GreaterThan(effect.Comp.StartTime));
            if (displaced)
            {
                Assert.That(origin, Is.Not.EqualTo(Vector2i.Zero));
                AdvanceTraversal(server, boss);
                Assert.That(entities.HasComponent<BossTraversalComponent>(boss), Is.False);
                Assert.That(effect.Comp.MovementCleave, Is.True, "Keep the full-size strike snapshot after the boss returns.");
            }
        });
    }

    [Test]
    public async Task CleaveAnimationPausesWithArenaAndExpiresAfterUnpause()
    {
        var (server, arena, players, _) = await Setup();
        EntityUid effect = default;
        TimeSpan start = default;
        float lifetime = 0;
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Frons, CastTime = 4, Damage = Hit(1) });
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            effect = server.EntMan.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact).Owner;
            start = server.EntMan.GetComponent<BossTelegraphComponent>(effect).StartTime;
            lifetime = server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.EqualTo(lifetime));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var animation = server.EntMan.GetComponent<BossTelegraphComponent>(effect);
            Assert.That(animation.StartTime, Is.GreaterThan(start));
            Assert.That((server.ResolveDependency<IGameTiming>().CurTime - animation.StartTime).TotalSeconds, Is.EqualTo(0).Within(0.05));
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(effect), Is.False));
    }

    [TestCase("cancel")] [TestCase("victory")] [TestCase("boss-deletion")] [TestCase("map-deletion")]
    public async Task CleaveAnimationCannotSurviveEncounterCleanupOrStartAfterCancellation(string reason)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var entities = server.EntMan;
            var bosses = server.System<BossSystem>();
            bosses.StartBossfight(players.ToList(), boss);
            server.System<BossMechanicSystem>().TryStart(boss, new BossTilePatternAttack
                { Telegraph = BossTelegraphPattern.Frons, CastTime = 4, Damage = Hit(1) });
            if (reason == "cancel")
            {
                bosses.CancelBossCast(boss);
                server.System<BossMechanicSystem>().Update(10);
                Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(entry => entry.Comp.Impact), Is.False);
                return;
            }
            entities.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            var effect = entities.AllEntities<BossTelegraphComponent>().Single(entry => entry.Comp.Impact).Owner;
            switch (reason)
            {
                case "victory": bosses.DamageBoss(boss, 100000); break;
                case "boss-deletion": entities.DeleteEntity(boss); break;
                case "map-deletion": entities.DeleteEntity(arena.Owner); break;
            }
            Assert.That(entities.EntityExists(effect), Is.False);
        });
    }

    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    public void CleaveAnimationStrokeHasFifteenDegreeOverrunAndExtendsBeyondArena(BossTelegraphPattern pattern)
    {
        var direction = pattern switch
        {
            BossTelegraphPattern.Sinistra => -Vector2.UnitX,
            BossTelegraphPattern.Dextra => Vector2.UnitX,
            BossTelegraphPattern.Tergum => -Vector2.UnitY,
            _ => Vector2.UnitY
        };
        Assert.That(Vector2.Distance(Vector2.Transform(Vector2.UnitY,
            Matrix3x2.CreateRotation(BossCleaveVisuals.Rotation(pattern, 0))), direction), Is.LessThan(0.00001));
        foreach (var facing in Enumerable.Range(0, 4))
        foreach (var origin in new[] { Vector2i.Zero, new Vector2i(3, -2) })
        {
            var danger = BossTilePatterns.Danger(pattern, [], facing: facing, origin: origin).ToHashSet();
            var mask = BossCleaveAnimationOverlay.MaskRows(danger);
            var initialRadius = BossCleaveVisuals.InitialRadius(5);
            var reach = BossCleaveAnimationOverlay.MaximumRadius(mask, origin) + BossCleaveVisuals.CornerPadding;
            var rotation = BossCleaveVisuals.Rotation(pattern, facing);
            var forward = Vector2.Transform(Vector2.UnitY, Matrix3x2.CreateRotation(rotation));
            var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
            for (var frame = 1; frame < 51; frame++)
            foreach (var sample in BossCleaveVisuals.Samples(frame / 60f))
            {
                var radius = BossCleaveVisuals.Radius(initialRadius, reach, sample.Time);
                var mesh = BossCleaveAnimationOverlay.StrokeMesh(origin, radius, 3f, rotation, BossCleaveVisuals.ArcRadians, -1.5f);
                foreach (var vertex in mesh)
                {
                    Assert.That(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y), Is.True);
                    Assert.That(Vector2.Distance(vertex, center), Is.InRange(0, reach + 0.01f));
                    Assert.That(Vector2.Dot(Vector2.Normalize(vertex - center), forward),
                        Is.GreaterThanOrEqualTo(MathF.Cos(105f * MathF.PI / 180) - 0.00001f),
                        "The visual extends exactly fifteen degrees beyond either end of the attacked half.");
                }
            }
            var finalMesh = BossCleaveAnimationOverlay.StrokeMesh(origin, reach, 3f, rotation, BossCleaveVisuals.ArcRadians, -1.5f);
            foreach (var endpoint in new[] { finalMesh[5], finalMesh[^2] })
                Assert.That(MathF.Acos(Vector2.Dot(Vector2.Normalize(endpoint - center), forward)) * 180 / MathF.PI,
                    Is.EqualTo(105f).Within(0.001f), "The two endpoints span the requested 210 degrees.");
            Assert.That(finalMesh.Any(point => point.X < -5.1f || point.X > 6.1f ||
                point.Y < -5.1f || point.Y > 6.1f), Is.True,
                "The visual stroke must continue outside the arena instead of being cut into a square.");
        }
        var centeredMesh = BossCleaveAnimationOverlay.StrokeMesh(Vector2i.Zero,
            BossCleaveVisuals.InitialRadius(5), 3f, BossCleaveVisuals.Rotation(pattern, 0), BossCleaveVisuals.ArcRadians, -1.5f);
        Assert.That(centeredMesh.Min(point => Vector2.Distance(point, new Vector2(0.5f))), Is.LessThan(1.6f),
            "Keep the broad violet body behind the bright leading edge.");
        Assert.That(BossCleaveVisuals.Samples(-0.1f), Is.Empty);
        Assert.That(BossCleaveVisuals.Samples(BossCleaveVisuals.DurationSeconds), Is.Empty);
    }

    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    public void CleaveAnimationStrokeKeepsItsThickStartAndReachesDangerCorners(BossTelegraphPattern pattern)
    {
        Assert.That(BossCleaveVisuals.Progress(0), Is.Zero);
        Assert.That(BossCleaveVisuals.Progress(BossCleaveVisuals.ExpandSeconds), Is.EqualTo(1));
        var initialRadius = BossCleaveVisuals.InitialRadius(5);
        Assert.That(initialRadius, Is.InRange(4.49f, 4.5f));
        Assert.That(BossCleaveVisuals.Radius(initialRadius, 8f, BossCleaveVisuals.AppearSeconds), Is.EqualTo(initialRadius));
        Assert.That(BossCleaveVisuals.Samples(2 / 60f).Single(sample => sample.Leading).Alpha, Is.EqualTo(1));
        Assert.That(BossCleaveVisuals.Samples(0).All(sample => sample.Alpha == 0), Is.True);
        var previous = 0f;
        for (var frame = 1; frame < 51; frame++)
        {
            var progress = BossCleaveVisuals.Progress(frame / 60f);
            Assert.That(progress, Is.GreaterThanOrEqualTo(previous));
            previous = progress;
        }
        foreach (var facing in Enumerable.Range(0, 4))
        foreach (var origin in new[] { Vector2i.Zero, new Vector2i(3, -2) })
        {
            var mask = BossCleaveAnimationOverlay.MaskRows(
                BossTilePatterns.Danger(pattern, [], facing: facing, origin: origin));
            var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
            var reach = BossCleaveAnimationOverlay.MaximumRadius(mask, origin) + BossCleaveVisuals.CornerPadding;
            Assert.That(BossCleaveVisuals.Radius(initialRadius, reach, BossCleaveVisuals.ExpandSeconds), Is.EqualTo(reach));
            var rotation = BossCleaveVisuals.Rotation(pattern, facing);
            var edge = BossCleaveAnimationOverlay.StrokeMesh(origin, reach, 0.12f, rotation, BossCleaveVisuals.ArcRadians, -0.06f);
            var corners = mask.SelectMany(bounds => new[]
            {
                new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom),
                new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top)
            }).Where(point => Vector2.Distance(point, center) >= reach - 0.03f);
            foreach (var corner in corners)
                Assert.That(CleaveMeshContains(edge, Vector2.Lerp(corner, center, 0.001f)), Is.True,
                    "The curved edge must still reach the farthest danger corner, including displaced casts.");
        }
    }

    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    public void CleaveAnimationStrokeKeepsFullWidthAndFadesFromItsStartingEnd(BossTelegraphPattern pattern)
    {
        foreach (var facing in Enumerable.Range(0, 4))
        foreach (var origin in new[] { Vector2i.Zero, new Vector2i(3, -2) })
        {
            var mask = BossCleaveAnimationOverlay.MaskRows(
                BossTilePatterns.Danger(pattern, [], facing: facing, origin: origin));
            var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
            var rotation = BossCleaveVisuals.Rotation(pattern, facing);
            var initialRadius = BossCleaveVisuals.InitialRadius(5);
            var reach = BossCleaveAnimationOverlay.MaximumRadius(mask, origin) + BossCleaveVisuals.CornerPadding;
            var startingAngle = rotation + MathF.PI / 2 - BossCleaveVisuals.ArcRadians / 2;
            var finishingAngle = rotation + MathF.PI / 2 + BossCleaveVisuals.ArcRadians / 2;
            var startingAxis = new Vector2(MathF.Cos(startingAngle), MathF.Sin(startingAngle));
            var finishingAxis = new Vector2(MathF.Cos(finishingAngle), MathF.Sin(finishingAngle));
            var previousMiddleAlpha = 1f;
            foreach (var time in new[] { 0f, 0.2f, BossCleaveVisuals.ExpandSeconds })
            {
                var radius = BossCleaveVisuals.Radius(initialRadius, reach, time);
                var vertices = BossCleaveAnimationOverlay.StrokeVertices(origin, radius, 3f, rotation,
                    Color.White.WithAlpha(0.8f), time, BossCleaveVisuals.ArcRadians, -1.5f);
                Assert.That(vertices.Length, Is.EqualTo(75 * 6));
                foreach (var segment in Enumerable.Range(0, 75))
                    Assert.That(Vector2.Distance(vertices[segment * 6].Position, vertices[segment * 6 + 5].Position),
                        Is.InRange(2.99f, 3.01f), "Restore the even-width band, including its faded end.");
                Assert.That(Vector2.Distance(Vector2.Normalize(vertices[5].Position - center), startingAxis), Is.LessThan(0.00001f));
                Assert.That(Vector2.Distance(Vector2.Normalize(vertices[^2].Position - center), finishingAxis), Is.LessThan(0.00001f),
                    "The endpoints must stay fixed while the whole 210-degree arc expands.");
                var beginning = vertices[0].Color.A;
                var middle = vertices[37 * 6].Color.A;
                var finish = vertices[^2].Color.A;
                Assert.That(beginning, Is.LessThan(finish * 0.06f), "The earlier cut must look almost faded out.");
                Assert.That(finish, Is.EqualTo(0.8f).Within(0.00001f), "The freshly cut finish keeps its full brightness.");
                Assert.That(middle, Is.GreaterThan(beginning).And.LessThan(finish));
                Assert.That(middle, Is.LessThanOrEqualTo(previousMiddleAlpha), "Older portions keep fading without a moving highlight.");
                previousMiddleAlpha = middle;
                foreach (var segment in Enumerable.Range(0, 74))
                    Assert.That(vertices[segment * 6].Color.A, Is.LessThanOrEqualTo(vertices[(segment + 1) * 6].Color.A),
                        "Brightness must rise smoothly from the beginning of the slash to the finish.");
            }
        }
    }

    [TestCase(BossTelegraphPattern.Sinistra)] [TestCase(BossTelegraphPattern.Dextra)]
    [TestCase(BossTelegraphPattern.Frons)] [TestCase(BossTelegraphPattern.Tergum)]
    public void CleaveAnimationApprovedTextureKeepsItsOffCentrePivotAndReadableTiming(BossTelegraphPattern pattern)
    {
        Assert.That(BossCleaveVisuals.DurationSeconds, Is.EqualTo(0.7f).Within(0.00001f));
        Assert.That(BossCleaveVisuals.ExpandSeconds, Is.EqualTo(0.4f));
        Assert.That(BossCleaveVisuals.TextureAlpha(-0.01f), Is.Zero);
        Assert.That(BossCleaveVisuals.TextureAlpha(0), Is.Zero);
        Assert.That(BossCleaveVisuals.TextureAlpha(1 / 60f), Is.EqualTo(1));
        Assert.That(BossCleaveVisuals.TextureAlpha(0.4f), Is.EqualTo(1).Within(0.00001f));
        Assert.That(BossCleaveVisuals.TextureAlpha(0.55f), Is.EqualTo(0.5f).Within(0.00001f));
        Assert.That(BossCleaveVisuals.TextureAlpha(0.625f), Is.EqualTo(0.15625f).Within(0.00001f));
        Assert.That(BossCleaveVisuals.TextureAlpha(0.699f), Is.LessThan(0.0001f));
        Assert.That(BossCleaveVisuals.TextureAlpha(0.7f), Is.LessThan(0.0000001f));
        Assert.That(BossCleaveVisuals.TextureAlpha(BossCleaveVisuals.DurationSeconds), Is.Zero);
        var size = new Vector2i(1254, 1254);
        var anchor = new Vector2(575, 627);
        foreach (var facing in Enumerable.Range(0, 4))
        foreach (var origin in new[] { Vector2i.Zero, new Vector2i(3, -2) })
        foreach (var scale in new[] { 0.7f, 1f })
        {
            var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
            var mask = BossCleaveAnimationOverlay.MaskRows(BossTilePatterns.Danger(pattern, [], facing: facing, origin: origin));
            var initial = BossCleaveVisuals.InitialRadius(5);
            var reach = BossCleaveAnimationOverlay.MaximumRadius(mask, origin) + BossCleaveVisuals.CornerPadding;
            foreach (var time in new[] { 0f, 0.2f, 0.4f })
            {
                var radius = BossCleaveVisuals.Radius(initial, reach, time) * scale;
                var bounds = BossCleaveAnimationOverlay.TextureBounds(size, anchor, 550, radius);
                var pivot = new Vector2(bounds.Left, bounds.Bottom) +
                    new Vector2(anchor.X, size.Y - anchor.Y) * (bounds.Width / size.X);
                Assert.That(pivot.Length(), Is.LessThan(0.00001f),
                    "Anchor the actual curved artwork at the boss, rather than centring its square canvas.");
                var rotation = BossCleaveVisuals.Rotation(pattern, facing);
                var transform = Matrix3x2.CreateRotation(rotation + MathF.PI / 2) * Matrix3x2.CreateTranslation(center);
                Assert.That(Vector2.Distance(Vector2.Transform(pivot, transform), center), Is.LessThan(0.00001f));
                var artFront = new Vector2(radius, 0);
                var expectedFront = center + Vector2.Transform(Vector2.UnitY, Matrix3x2.CreateRotation(rotation)) * radius;
                Assert.That(Vector2.Distance(Vector2.Transform(artFront, transform), expectedFront), Is.LessThan(0.00001f));
                Assert.That(bounds.Width / bounds.Height, Is.EqualTo(1).Within(0.00001f), "Keep all approved artwork proportions.");
                var fullBounds = BossCleaveAnimationOverlay.TextureBounds(size, anchor, 550, radius / scale);
                Assert.That(bounds.Width / fullBounds.Width, Is.EqualTo(scale).Within(0.00001f));
            }
        }
    }

    private static bool CleaveMeshContains(Vector2[] mesh, Vector2 point)
    {
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        for (var index = 0; index < mesh.Length; index += 3)
        {
            var a = mesh[index];
            var b = mesh[index + 1];
            var c = mesh[index + 2];
            if (MathF.Abs(Cross(b - a, c - a)) < 0.000001f) continue;
            var ab = Cross(b - a, point - a);
            var bc = Cross(c - b, point - b);
            var ca = Cross(a - c, point - c);
            if (ab >= -0.00001f && bc >= -0.00001f && ca >= -0.00001f ||
                ab <= 0.00001f && bc <= 0.00001f && ca <= 0.00001f) return true;
        }
        return false;
    }
}
