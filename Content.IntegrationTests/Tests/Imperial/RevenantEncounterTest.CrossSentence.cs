using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public async Task MixedOrdersAlternateAcrossSlotsLapsAndCancelledRetries(int slots, bool initialStack)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Escalation = Enumerable.Range(0, slots).Select(slot => new BossEncounterStep
                { MixedSentence = true, StackFirst = initialStack ^ (slot % 2 != 0) }).ToList();
            server.EntMan.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var encounter = server.System<BossEncounterSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            var timing = server.ResolveDependency<IGameTiming>();
            for (var group = 0; group < 8; group++)
            {
                rotation.NextStep = timing.CurTime;
                encounter.Update(0);
                var expectedStack = initialStack ^ (group % 2 != 0);
                var first = expectedStack ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread;
                Assert.That(rotation.PendingSteps[1].Pattern, Is.EqualTo(first));
                Assert.That(rotation.PendingSteps[3].Pattern, Is.Not.EqualTo(first));
                if (group == 1)
                {
                    var previousOrder = rotation.LastMixedStackFirst;
                    server.System<BossSystem>().CancelBossCast(boss);
                    Assert.That(rotation.LastMixedStackFirst, Is.EqualTo(previousOrder), "Cancellation cannot consume alternation.");
                    rotation.NextStep = timing.CurTime;
                    encounter.Update(0);
                    Assert.That(rotation.PendingSteps[1].Pattern, Is.EqualTo(first));
                }
                while (rotation.PendingSteps.Count > 0)
                {
                    server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = timing.CurTime;
                    mechanics.Update(0);
                    if (rotation.PendingSteps.Count == 0) break;
                    rotation.NextStep = timing.CurTime;
                    encounter.Update(0);
                }
                Assert.That(rotation.LastMixedStackFirst, Is.EqualTo(expectedStack));
                Assert.That(rotation.PendingMixedStackFirst, Is.Null);
                Assert.That(rotation.Step, Is.EqualTo((group + 1) % slots));
                Assert.That(rotation.Lap, Is.EqualTo((group + 1) / slots));
                foreach (var player in players)
                    server.System<DamageableSystem>().SetAllDamage(player,
                        server.EntMan.GetComponent<DamageableComponent>(player), FixedPoint2.Zero);
            }
            encounter.Begin(boss);
            Assert.That(rotation.LastMixedStackFirst, Is.Null, "A fresh attempt has no previous mixed order.");
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    public async Task EscalationCrossOrDivideResolvesRememberedMarksSimultaneously(int lap, bool stack)
    {
        var (server, arena, originalPlayers, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var extra = entities.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            entities.RemoveComponent<ShieldOnStartupComponent>(extra);
            entities.RemoveComponent<PassiveDamageComponent>(extra);
            entities.EnsureComponent<BossArenaPartyMemberComponent>(extra);
            var players = originalPlayers.Append(extra).ToArray();
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = entities.GetComponent<BossEncounterComponent>(boss);
            Assert.That(rotation.Tutorial.Where(step => step.Pattern is BossTelegraphPattern.Cross or BossTelegraphPattern.Divide)
                .All(step => !step.CrossSentence), Is.True);
            Assert.That(rotation.Rotation.Where(step => step.Pattern == BossTelegraphPattern.Cross)
                .All(step => !step.CrossSentence), Is.True);
            Assert.That(rotation.Escalation.Where(step => step.VigilFollowup != null)
                .All(step => !step.CrossSentence), Is.True);
            var definition = rotation.Escalation.Single(step => step.CrossSentence);
            var targeted = stack ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread;
            definition.CrossSentenceTarget = targeted;
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Lap = lap;
            rotation.Escalation = [definition];
            entities.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var encounter = server.System<BossEncounterSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            var telegraphs = server.System<BossTelegraphSystem>();
            var timing = server.ResolveDependency<IGameTiming>();
            rotation.NextStep = timing.CurTime;
            encounter.Update(0);
            var pattern = lap == 0 ? BossTelegraphPattern.Cross : BossTelegraphPattern.Divide;
            Assert.That(rotation.PendingSteps, Has.Count.EqualTo(3));
            var preview = entities.GetComponent<BossMechanicComponent>(boss);
            Assert.That(preview.Attack.Telegraph, Is.EqualTo(targeted));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString(stack ? "Revenant_Boss_Sentencing_Communion" : "Revenant_Boss_Sentencing_Severance")));
            var stored = rotation.StoredTargets[targeted];
            var targets = stored.Targets.ToArray();
            var marks = stored.Marks.Values.ToArray();
            Assert.That(targets, Has.Length.EqualTo(stack ? 1 : 3));
            Assert.That(targets, Does.Not.Contain(players[0]));
            Assert.That(preview.PreloadOnly, Is.True);
            Assert.That((preview.Deadline - preview.Start).TotalSeconds, Is.EqualTo(3));
            var spreadWarning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            Assert.That(spreadWarning.Preload, Is.True);
            Assert.That(spreadWarning.ShowDanger(preview.Start), Is.True);
            Assert.That(spreadWarning.ShowDanger(preview.Start + TimeSpan.FromSeconds(3)), Is.False);
            preview.Deadline = timing.CurTime;
            mechanics.Update(0);
            foreach (var player in players) Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            rotation.NextStep = timing.CurTime;
            encounter.Update(0);
            var crossPreview = entities.GetComponent<BossMechanicComponent>(boss);
            var crossWarning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            Assert.That(crossPreview.Attack.Telegraph, Is.EqualTo(pattern));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString(server.System<BossMechanicSystem>().CastNameKey(boss, pattern, sentencing: true))));
            Assert.That(crossPreview.PreloadOnly, Is.True);
            Assert.That((crossPreview.Deadline - crossPreview.Start).TotalSeconds, Is.EqualTo(5));
            Assert.That(crossWarning.Preload, Is.True);
            Assert.That(crossWarning.ShowDanger(crossPreview.Start), Is.True);
            Assert.That(crossWarning.Danger, Is.EquivalentTo(BossTilePatterns.Danger(pattern, [])));
            Assert.That(rotation.StoredTargets[targeted].Targets, Is.EqualTo(targets));
            foreach (var mark in marks) Assert.That(entities.EntityExists(mark), Is.True);
            crossPreview.Deadline = timing.CurTime;
            mechanics.Update(0);
            foreach (var player in players) Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            rotation.NextStep = timing.CurTime;
            encounter.Update(0);
            var cross = entities.GetComponent<BossMechanicComponent>(boss);
            Assert.That(cross.Attack.Telegraph, Is.EqualTo(pattern));
            Assert.That(cross.SimultaneousAttack?.Telegraph, Is.EqualTo(targeted));
            Assert.That(cross.Targets, Is.EqualTo(targets));
            Assert.That((cross.Deadline - cross.Start).TotalSeconds, Is.EqualTo(7));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Visible, Is.True);
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString("Revenant_Boss_Requiem")));
            var releaseWarning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            Assert.That(releaseWarning.StoredStrike, Is.True);
            Assert.That(releaseWarning.ShowDanger(cross.Start), Is.False);
            Assert.That((releaseWarning.EndTime - releaseWarning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
            var safeCenters = lap == 0 ? new Vector2i[] { new(-4, -2), new(4, -4), new(-4, 4) }
                : new Vector2i[] { new(-4, 0), new(4, 0), new(0, -4) };
            for (var i = 0; i < targets.Length; i++)
                server.System<SharedTransformSystem>().SetCoordinates(targets[i],
                    new EntityCoordinates(arena.Comp.CombatGrid, safeCenters[i].X + 0.5f, safeCenters[i].Y + 0.5f));
            var unmarked = players.Where(player => player != players[0] && !targets.Contains(player)).ToArray();
            foreach (var player in unmarked)
                server.System<SharedTransformSystem>().SetCoordinates(player,
                    new EntityCoordinates(arena.Comp.CombatGrid, safeCenters[0].X + 1.5f,
                        safeCenters[0].Y + 0.5f + (stack ? 0 : lap == 0 ? 1 : 2)));
            server.System<SharedTransformSystem>().SetCoordinates(players[0],
                new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, lap == 0 ? 0.5f : 4.5f));
            foreach (var mark in marks) Assert.That(entities.EntityExists(mark), Is.True);
            var targetTiles = targets.Select(target => { telegraphs.TryGetPlayerTile(boss, target, out var tile); return tile; }).ToArray();
            var expectedDanger = BossTilePatterns.Danger(pattern, []).Union(BossTilePatterns.Danger(targeted, targetTiles));
            Assert.That(telegraphs.SnapshotDanger(boss), Is.EquivalentTo(expectedDanger));
            cross.Deadline = timing.CurTime;
            mechanics.Update(0);
            Assert.That(entities.GetComponent<DamageableComponent>(players[0]).TotalDamage, Is.EqualTo(FixedPoint2.New(stack ? 2 : 1)));
            foreach (var player in unmarked)
                Assert.That(entities.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.New(stack ? 0 : 2)));
            Assert.That(entities.HasComponent<BossMechanicComponent>(boss), Is.False, "Both patterns finish in one update.");
            Assert.That(rotation.NextStep, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(7)));
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
            Assert.That(entities.GetComponent<BossCastBarComponent>(boss).CastName, Is.Empty);
            Assert.That(telegraphs.GetTelegraph(boss), Is.Null);
            var effects = entities.AllEntities<MetaDataComponent>()
                .Where(entity => entity.Comp.EntityPrototype?.ID == "RevenantBossTileHitEffect").ToArray();
            var riverTiles = BossTilePatterns.Danger(pattern, []);
            Assert.That(effects, Has.Length.EqualTo(stack ? expectedDanger.Except(riverTiles).Count() : 0),
                "Spread uses its soul burst; stack retains its separate targeted tile effects.");
            var impact = entities.AllEntities<BossTelegraphComponent>().Single(e => e.Comp.Impact).Comp;
            Assert.That(impact.SpreadCenters, Is.EqualTo(stack ? Array.Empty<Vector2i>() : targetTiles));
            Assert.That((impact.EndTime - impact.StartTime).TotalSeconds, Is.EqualTo(2.2).Within(.00001),
                "Adding a shorter spread burst must not cut off the river fade.");
            foreach (var effect in effects)
                Assert.That(entities.HasComponent<DamageableComponent>(effect.Owner), Is.False);
            foreach (var target in targets) Assert.That(entities.GetComponent<DamageableComponent>(target).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            foreach (var mark in marks) Assert.That(entities.EntityExists(mark), Is.False);
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.LastMixedStackFirst, Is.Null, "Cross's spreads do not consume the mixed-order alternation.");
        });
    }

    [TestCase("cancel")]
    [TestCase("wipe")]
    [TestCase("victory")]
    [TestCase("map-deletion")]
    public async Task CrossSentenceInterruptionDiscardsTheQueuedSpreadAndMarks(string exit)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Escalation = [new() { Pattern = BossTelegraphPattern.Cross, CrossSentence = true }];
            server.EntMan.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var timing = server.ResolveDependency<IGameTiming>();
            rotation.NextStep = timing.CurTime;
            server.System<BossEncounterSystem>().Update(0);
            for (var preview = 0; preview < 2; preview++)
            {
                server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = timing.CurTime;
                server.System<BossMechanicSystem>().Update(0);
                rotation.NextStep = timing.CurTime;
                server.System<BossEncounterSystem>().Update(0);
            }
            var marks = rotation.StoredTargets.Values.SelectMany(stored => stored.Marks.Values).ToArray();
            switch (exit)
            {
                case "cancel": server.System<BossSystem>().CancelBossCast(boss); break;
                case "wipe":
                    foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);
                    server.System<BossEncounterSystem>().Update(0);
                    break;
                case "victory": server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2); break;
                default: server.EntMan.DeleteEntity(arena.Owner); break;
            }
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.PendingSteps, Is.Empty);
            foreach (var marker in marks) Assert.That(server.EntMan.EntityExists(marker), Is.False);
            foreach (var player in players.Where(player => server.EntMan.EntityExists(player)))
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
