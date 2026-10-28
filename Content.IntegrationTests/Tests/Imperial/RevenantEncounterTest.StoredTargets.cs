using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    private static BossEncounterComponent StartStoredSentence(ServerIntegrationInstance server,
        EntityUid boss, EntityUid[] players, int previewIndex)
    {
        server.System<BossSystem>().StartBossfight(players.ToList(), boss);
        var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
        foreach (var key in rotation.Damage.Keys.ToArray()) rotation.Damage[key] = Hit(1);
        rotation.Repeating = rotation.Escalating = true;
        rotation.Escalation = [new() { MixedSentence = true }];
        server.EntMan.EnsureComponent<BossFocusMarkerComponent>(boss).Tank = players[0];
        for (var index = 0; index <= previewIndex; index++)
        {
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            if (index == previewIndex) break;
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
        }
        return rotation;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LostRememberedCommunionTargetRestartsPreparationWithoutSilentRetargeting(bool leaves)
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var rotation = StartStoredSentence(server, boss, players, 4);
            var oldMarks = rotation.StoredTargets.Values.SelectMany(stored => stored.Marks.Values).ToArray();
            var target = rotation.StoredTargets[BossTelegraphPattern.Stack].Targets.Single();
            if (leaves)
                server.System<SharedTransformSystem>().SetCoordinates(target,
                    server.EntMan.GetComponent<TransformComponent>(gate).Coordinates);
            else
                server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.Step, Is.Zero);
            Assert.That(rotation.Lap, Is.Zero);
            Assert.That(rotation.Waiting, Is.False);
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss), Is.False);
            foreach (var marker in oldMarks) Assert.That(server.EntMan.EntityExists(marker), Is.False);
            Assert.That(rotation.NextStep, Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.PendingIndex, Is.Zero);
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).PreloadOnly, Is.True);
            Assert.That(rotation.StoredTargets, Is.Empty, "New assignments require new visible previews.");
        });
    }

    [Test]
    public async Task LosingTheOnlyEligibleFutureStackTargetRetriesWithFreshAssignments()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var rotation = StartStoredSentence(server, boss, players, 1);
            var spread = rotation.StoredTargets[BossTelegraphPattern.Spread];
            var futureStack = players.Except(spread.Targets).Single(player => player != players[0]);
            var marks = spread.Marks.Values.ToArray();
            server.System<MobStateSystem>().ChangeMobState(futureStack, MobState.Dead);
            var timing = server.ResolveDependency<IGameTiming>();
            for (var i = 0; i < 2; i++)
            {
                server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline = timing.CurTime;
                server.System<BossMechanicSystem>().Update(0);
                rotation.NextStep = timing.CurTime;
                server.System<BossEncounterSystem>().Update(0);
            }
            Assert.That(rotation.PendingSteps, Is.Empty);
            Assert.That(rotation.StoredTargets, Is.Empty);
            foreach (var marker in marks) Assert.That(server.EntMan.EntityExists(marker), Is.False);
            Assert.That(rotation.FightRunning, Is.True);
            Assert.That(rotation.NextStep, Is.GreaterThan(timing.CurTime));
            rotation.NextStep = timing.CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(server.EntMan.GetComponent<BossMechanicComponent>(boss).PreloadOnly, Is.True);
        });
    }

    [Test]
    public async Task PausedSentencingPreviewKeepsMarksAndResumesIntoTheNextCleave()
    {
        var (server, arena, players, _) = await Setup();
        TimeSpan deadline = default;
        EntityUid[] marks = [];
        await server.WaitAssertion(() =>
        {
            var rotation = StartStoredSentence(server, arena.Comp.Boss, players, 1);
            var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(arena.Comp.Boss);
            deadline = mechanic.Deadline;
            Assert.That((deadline - mechanic.Start).TotalSeconds, Is.EqualTo(3));
            marks = rotation.StoredTargets.Values.SelectMany(stored => stored.Marks.Values).ToArray();
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var rotation = server.EntMan.GetComponent<BossEncounterComponent>(boss);
            var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
            Assert.That(mechanic.Deadline, Is.EqualTo(deadline));
            Assert.That(rotation.PendingIndex, Is.EqualTo(1));
            foreach (var marker in marks) Assert.That(server.EntMan.EntityExists(marker), Is.True);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That((mechanic.Deadline - deadline).TotalSeconds, Is.GreaterThan(1));
            Assert.That(warning.EndTime, Is.EqualTo(mechanic.Deadline));
            Assert.That(warning.ShowDanger(mechanic.Deadline - TimeSpan.FromMilliseconds(1)), Is.True);
            Assert.That(warning.ShowDanger(mechanic.Deadline), Is.False);
            mechanic.Deadline = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
            rotation.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(rotation.PendingIndex, Is.EqualTo(2));
            foreach (var marker in marks) Assert.That(server.EntMan.EntityExists(marker), Is.True);
        });
    }

    [TestCase("victory")]
    [TestCase("wipe")]
    [TestCase("boss-deletion")]
    [TestCase("map-deletion")]
    public async Task EncounterExitDiscardsBothRememberedAssignments(string exit)
    {
        var (server, arena, players, _) = await Setup();
        EntityUid[] marks = [];
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var rotation = StartStoredSentence(server, boss, players, 4);
            Assert.That(rotation.StoredTargets.Count, Is.EqualTo(2));
            marks = rotation.StoredTargets.Values.SelectMany(stored => stored.Marks.Values).ToArray();
            switch (exit)
            {
                case "victory": server.System<BossSystem>().DamageBoss(boss, server.EntMan.GetComponent<BossComponent>(boss).MaxHealth * 2); break;
                case "wipe":
                    foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);
                    server.System<BossEncounterSystem>().Update(0);
                    break;
                default: server.EntMan.DeleteEntity(exit == "boss-deletion" ? boss : arena.Owner); break;
            }
            Assert.That(rotation.StoredTargets, Is.Empty);
            Assert.That(rotation.PendingSteps, Is.Empty);
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            foreach (var marker in marks) Assert.That(server.EntMan.EntityExists(marker), Is.False);
        });
    }
}
