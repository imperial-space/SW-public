using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task CardinalCastsAndSentencingsStayAlignedWithArenaWhenBossTurns()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            server.System<SharedTransformSystem>().SetLocalRotation(boss, Angle.FromDegrees(90));
            var mechanics = server.System<BossMechanicSystem>();
            var telegraphs = server.System<BossTelegraphSystem>();
            (BossTelegraphPattern Pattern, string Name, Vector2i Unsafe, Vector2i Safe)[] directions =
            [
                (BossTelegraphPattern.Frons, "Septentrio", new(0, 4), new(0, -4)),
                (BossTelegraphPattern.Dextra, "Oriens", new(4, 0), new(-4, 0)),
                (BossTelegraphPattern.Tergum, "Meridies", new(0, -4), new(0, 4)),
                (BossTelegraphPattern.Sinistra, "Occidens", new(-4, 0), new(4, 0))
            ];
            foreach (var (pattern, name, unsafeTile, safeTile) in directions)
            {
                Assert.That(mechanics.TryStart(boss, new BossTilePatternAttack
                    { Telegraph = pattern, CastTime = 3, Damage = Hit(1) }), Is.True);
                var warning = telegraphs.GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.Facing, Is.Zero);
                Assert.That(warning.Danger, Does.Contain(unsafeTile).And.Not.Contain(safeTile));
                Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName, Is.EqualTo(name));
                mechanics.Cancel(boss);
            }
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss,
                new BossPreparedSequenceAttack(), preview: true,
                queue: [BossTelegraphPattern.Frons, BossTelegraphPattern.Dextra]), Is.True);
            Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).Facing, Is.Zero);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString("Revenant_Boss_Sentencing_Septentrio")));
            server.System<BossSequenceSystem>().Cancel(boss);
        });
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    public async Task MixedSentenceRemembersThreeCleavesAndSeparatesSpreadAndStack(bool stackFirst, int lap)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Lap = lap;
            rotation.Escalation = [new() { MixedSentence = true, StackFirst = stackFirst }];
            server.EntMan.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
            var sequences = server.System<BossSequenceSystem>();
            sequences.RecordStrike(boss, BossTelegraphPattern.Frons);
            sequences.RecordStrike(boss, BossTelegraphPattern.Frons);
            var encounter = server.System<BossEncounterSystem>();
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.Update(0);
            var pending = rotation.PendingSteps.ToArray();
            var directions = pending.Take(5).Where(step => BossSequenceSystem.IsCleave(step.Pattern)).Select(step => step.Pattern).ToArray();
            Assert.That(pending, Has.Length.EqualTo(10));
            Assert.That(sequences.CanFollowHistory(boss, directions), Is.True);
            var expectedStackFirst = stackFirst ^ (lap % 2 != 0);
            var firstTargeted = expectedStackFirst ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread;
            var secondTargeted = expectedStackFirst ? BossTelegraphPattern.Spread : BossTelegraphPattern.Stack;
            Assert.That(pending.Select(step => step.Pattern), Is.EqualTo(new[]
                { directions[0], firstTargeted, directions[1], secondTargeted, directions[2],
                  directions[0], firstTargeted, directions[1], secondTargeted, directions[2] }));
            var remembered = new Dictionary<BossTelegraphPattern, Dictionary<EntityUid, EntityUid>>();
            for (var index = 0; index < pending.Length; index++)
            {
                var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
                Assert.That(rotation.Step, Is.Zero);
                Assert.That(rotation.PendingIndex, Is.EqualTo(index));
                Assert.That(warning.Pattern, Is.EqualTo(pending[index].Pattern));
                if (index == 5)
                    server.System<SharedTransformSystem>().SetCoordinates(remembered[firstTargeted].Keys.First(),
                        new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, -4.5f));
                if (index < 5)
                {
                    Assert.That(warning.Preload && mechanic.PreloadOnly, Is.True);
                    Assert.That(warning.ShowDanger(mechanic.Start), Is.True);
                    var targeted = index is 1 or 3;
                    Assert.That(bar.CastName, Is.EqualTo(Loc.GetString(
                        server.System<BossMechanicSystem>().CastNameKey(boss, warning.Pattern, sentencing: true))));
                    Assert.That((mechanic.Deadline - mechanic.Start).TotalSeconds, Is.EqualTo(targeted ? 3 : 5));
                    Assert.That(warning.ShowDanger(mechanic.Start + TimeSpan.FromSeconds(targeted ? 3 : 5)), Is.False);
                    if (targeted)
                    {
                        var stored = rotation.StoredTargets[warning.Pattern];
                        Assert.That(stored.Targets, Does.Not.Contain(players[0]));
                        Assert.That(stored.Targets.Count, Is.EqualTo(warning.Pattern == BossTelegraphPattern.Stack ? 1 : 2));
                        remembered[warning.Pattern] = new(stored.Marks);
                        Assert.That(mechanic.Targets, Is.EqualTo(stored.Targets));
                    }
                }
                else if (index is 5 or 7 or 9)
                {
                    Assert.That(warning.Pattern, Is.EqualTo(directions[(index - 5) / 2]));
                    Assert.That(warning.StoredStrike, Is.True);
                    Assert.That(warning.ShowDanger(mechanic.Start), Is.False);
                    Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
                    Assert.That(bar.Visible, Is.EqualTo(index == 5));
                    Assert.That(bar.CastName, Is.EqualTo(index == 5 ? Loc.GetString("Revenant_Boss_Dire_Requiem") : ""));
                    Assert.That((mechanic.Deadline - mechanic.Start).TotalSeconds, Is.EqualTo(index == 5 ? 7 : 5));
                }
                else
                {
                    var stack = index == 6 ? expectedStackFirst : !expectedStackFirst;
                    Assert.That(warning.Pattern, Is.EqualTo(stack ? BossTelegraphPattern.Stack : BossTelegraphPattern.Spread));
                    Assert.That(mechanic.Targets, Does.Not.Contain(players[0]));
                    Assert.That(mechanic.Targets.Count, Is.EqualTo(stack ? 1 : 2));
                    Assert.That(bar.Visible, Is.False);
                    Assert.That(bar.CastName, Is.Empty);
                    Assert.That(mechanic.Targets, Is.EqualTo(remembered[warning.Pattern].Keys));
                    Assert.That(warning.StoredStrike, Is.True);
                    Assert.That(warning.ShowDanger(mechanic.Start), Is.False);
                    Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
                    Assert.That((mechanic.Deadline - mechanic.Start).TotalSeconds, Is.EqualTo(5));
                    var targetTiles = mechanic.Targets.Select(target =>
                    {
                        Assert.That(server.System<BossTelegraphSystem>().TryGetPlayerTile(boss, target, out var tile), Is.True);
                        return tile;
                    }).ToArray();
                    Assert.That(server.System<BossTelegraphSystem>().SnapshotDanger(boss),
                        Is.EquivalentTo(BossTilePatterns.Danger(warning.Pattern, targetTiles)),
                        "Remembered assignments resolve around current positions, not their preview locations.");
                }
                foreach (var stored in rotation.StoredTargets.Values)
                foreach (var (target, marker) in stored.Marks)
                {
                    Assert.That(server.EntMan.EntityExists(marker), Is.True);
                    Assert.That(server.EntMan.GetComponent<TransformComponent>(marker).ParentUid, Is.EqualTo(target));
                }
                var markerCount = server.EntMan.AllEntities<BossActorMarkerComponent>()
                    .Count(mark => mark.Comp.Boss == boss && mark.Comp.Kind != BossActorMarkerKind.Tank);
                Assert.That(markerCount, Is.EqualTo(rotation.StoredTargets.Values.Sum(stored => stored.Marks.Count)), "No transient duplicates.");
                mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossMechanicSystem>().Update(0);
                if (index < 5)
                    foreach (var player in players)
                        Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                if (index is 6 or 8)
                {
                    Assert.That(rotation.StoredTargets.ContainsKey(warning.Pattern), Is.False);
                    foreach (var marker in remembered[warning.Pattern].Values)
                        Assert.That(server.EntMan.EntityExists(marker), Is.False, "Only the resolved assignment loses its marks.");
                }
                foreach (var stored in rotation.StoredTargets)
                    Assert.That(stored.Value.Marks, Is.EqualTo(remembered[stored.Key]), "Marks persist through other previews and hits.");
                Assert.That(rotation.Waiting, Is.False);
                if (index == pending.Length - 1) break;
                Assert.That(rotation.PendingSteps.Count, Is.EqualTo(10), "One attack cannot consume the remaining remembered mechanics.");
                rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
                encounter.Update(0);
            }
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.Lap, Is.EqualTo(lap + 1));
            Assert.That(server.EntMan.GetComponent<BossSequenceHistoryComponent>(boss).LastStrikes, Is.EqualTo(directions.TakeLast(2)));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
        });
    }

    [TestCase(BossTelegraphPattern.Cross, 0)]
    [TestCase(BossTelegraphPattern.Divide, 0)]
    [TestCase(BossTelegraphPattern.Cross, 1)]
    [TestCase(BossTelegraphPattern.Divide, 1)]
    public async Task VigilPreviewsItsFollowupThenRestoresLightBeforeTheRememberedHit(BossTelegraphPattern followup, int lap)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            var alternate = followup == BossTelegraphPattern.Cross ? BossTelegraphPattern.Divide : BossTelegraphPattern.Cross;
            rotation.Escalation = [new() { VigilFollowup = followup, Alternate = alternate, CastTime = 6 }];
            rotation.Lap = lap;
            if (lap % 2 != 0) followup = alternate;
            var encounter = server.System<BossEncounterSystem>();
            var mechanics = server.System<BossMechanicSystem>();
            var telegraphs = server.System<BossTelegraphSystem>();
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.Update(0);
            var preload = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            Assert.That(preload.PreloadOnly, Is.True);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString(followup == BossTelegraphPattern.Cross
                    ? "Revenant_Boss_Cruciform_Vigil" : "Revenant_Boss_Sundered_Vigil")));
            Assert.That(telegraphs.GetTelegraph(boss)!.Value.Comp.ShowDanger(preload.Start), Is.True);
            preload.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.Update(0);
            var blackout = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            var corner = BossTilePatterns.BlackoutCenter(blackout.Attack.Telegraph!.Value);
            foreach (var player in players)
                server.System<SharedTransformSystem>().SetCoordinates(player,
                    new EntityCoordinates(arena.Comp.CombatGrid, corner.X + 0.5f, corner.Y + 0.5f));
            Assert.That(server.EntMan.GetComponent<BossPresentationComponent>(boss).Hidden, Is.True);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Visible, Is.False);
            blackout.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            Assert.That(blackout.BlackoutRecovery, Is.True);
            FinishVigilRecovery(server, boss);
            Assert.That(server.EntMan.GetComponent<BossPresentationComponent>(boss).Hidden, Is.False);
            Assert.That(server.EntMan.GetComponent<BossPresentationComponent>(boss).LightTo, Is.EqualTo(3));
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.Update(0);
            var strike = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            var warning = telegraphs.GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.Pattern, Is.EqualTo(followup));
            Assert.That(warning.StoredStrike, Is.True);
            Assert.That(warning.ShowDanger(strike.Start), Is.False);
            Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName, Is.Empty);
            Assert.That((strike.Deadline - strike.Start).TotalSeconds, Is.EqualTo(5));

            server.System<SharedTransformSystem>().SetCoordinates(players[0],
                new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, corner.Y + 0.5f));
            strike.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            for (var i = 0; i < players.Length; i++)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(),
                    Is.EqualTo(followup == BossTelegraphPattern.Cross ? (i == 0 ? 1 : 0) : (i == 0 ? 0 : 1)));
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.Lap, Is.EqualTo(lap + 1));
        });
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(7)]
    public async Task InterruptedCombinationDiscardsPendingStrikesAndRetriesFromPreparation(int cancelAt)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
            rotation.Repeating = rotation.Escalating = true;
            rotation.Escalation = [new() { MixedSentence = true }];
            for (var index = 0; index <= cancelAt; index++)
            {
                rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossEncounterSystem>().Update(0);
                if (index == cancelAt) break;
                server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossMechanicSystem>().Update(0);
            }
            Assert.That(server.System<BossSystem>().CancelBossCast(boss), Is.True);
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(server.EntMan.AllEntities<BossActorMarkerComponent>().Any(mark =>
                mark.Comp.Boss == boss && mark.Comp.Kind != BossActorMarkerKind.Tank), Is.False);
            Assert.That(rotation.Step, Is.Zero);
            Assert.That(rotation.Lap, Is.Zero);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            var before = players.Select(player => server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage).ToArray();
            server.System<BossMechanicSystem>().Update(30);
            Assert.That(players.Select(player => server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage), Is.EqualTo(before));
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.PendingIndex, Is.Zero);
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).PreloadOnly, Is.True);
            Assert.That(server.System<BossSequenceSystem>().CanFollowHistory(boss,
                rotation.PendingSteps.Take(5).Where(step => BossSequenceSystem.IsCleave(step.Pattern)).Select(step => step.Pattern).ToArray()), Is.True);
        });
    }
}
