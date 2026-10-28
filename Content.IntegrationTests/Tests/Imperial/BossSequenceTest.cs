using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossSequenceTest : RevenantIntegrationTestBase
{
    private static readonly BossTelegraphPattern L = BossTelegraphPattern.Sinistra;
    private static readonly BossTelegraphPattern R = BossTelegraphPattern.Dextra;

    [Test]
    public async Task MechanicNamedConsoleCommandsSelectPreviewsAndPreparedCombos()
    {
        var (server, arena, players) = await Setup();
        await server.WaitAssertion(() =>
        {
            var host = server.ResolveDependency<IConsoleHost>();
            var boss = arena.Comp.Boss;
            (string Command, BossTelegraphPattern Pattern, float Duration)[] previews =
            [
                ("crescentreap RevenantBoss sinistra 3", L, 3),
                ("crescentreap RevenantBoss DEXTRA", R, 5),
                ("crescentreap RevenantBoss front", BossTelegraphPattern.Frons, 5),
                ("crescentreap RevenantBoss tergum", BossTelegraphPattern.Tergum, 5),
                ("crescentreap RevenantBoss septentrio", BossTelegraphPattern.Frons, 5),
                ("crescentreap RevenantBoss oriens", R, 5),
                ("crescentreap RevenantBoss meridies", BossTelegraphPattern.Tergum, 5),
                ("crescentreap RevenantBoss occidens", L, 5),
                ("blackout RevenantBoss", BossTelegraphPattern.BlackoutNorthWest, 6),
                ("blackout RevenantBoss ne 7", BossTelegraphPattern.BlackoutNorthEast, 7),
                ("blackout RevenantBoss sw", BossTelegraphPattern.BlackoutSouthWest, 6),
                ("blackout RevenantBoss se", BossTelegraphPattern.BlackoutSouthEast, 6),
                ("midnightvigil RevenantBoss ne 7", BossTelegraphPattern.BlackoutNorthEast, 7),
                ("deathsembrace RevenantBoss", BossTelegraphPattern.Rejection, 6),
                ("deathsembrace RevenantBoss invitation 8", BossTelegraphPattern.Invitation, 8),
                ("sepulchralcross RevenantBoss 4", BossTelegraphPattern.Cross, 4),
                ("sepulchraldivide RevenantBoss", BossTelegraphPattern.Divide, 5),
                ("reaperssentence RevenantBoss", BossTelegraphPattern.Spread, 5),
                ("reaperssentence RevenantBoss stack 4", BossTelegraphPattern.Stack, 4),
                ("reaperssentence RevenantBoss severance 4", BossTelegraphPattern.Spread, 4),
                ("reaperssentence RevenantBoss communion 4", BossTelegraphPattern.Stack, 4),
                ("paleconstellation RevenantBoss", BossTelegraphPattern.BlueFirst, 6),
                ("paleconstellation RevenantBoss purple-first 7", BossTelegraphPattern.PurpleFirst, 7),
                ("paleconstellation RevenantBoss elegy 7", BossTelegraphPattern.BlueFirst, 7),
                ("paleconstellation RevenantBoss dirge 7", BossTelegraphPattern.PurpleFirst, 7),
                ("bosstelegraph RevenantBoss SEVERANCE 5", BossTelegraphPattern.Spread, 5),
                ("bosstelegraph RevenantBoss cross 5", BossTelegraphPattern.Cross, 5)
            ];
            foreach (var (command, pattern, duration) in previews)
            {
                var name = command.Split(' ')[0];
                Assert.That(host.AvailableCommands[name].Help, Is.Not.Empty);
                Assert.That(host.AvailableCommands[name].Description, Is.Not.Empty);
                host.LocalShell.ExecuteCommand(command);
                Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Pattern,
                    Is.EqualTo(pattern), command);
                var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
                Assert.That(bar.EndTime - bar.StartTime, Is.EqualTo(TimeSpan.FromSeconds(duration)), command);
                var thematicSuffix = pattern switch
                {
                    BossTelegraphPattern.Sinistra => "Occidens",
                    BossTelegraphPattern.Dextra => "Oriens",
                    BossTelegraphPattern.Frons => "Septentrio",
                    BossTelegraphPattern.Tergum => "Meridies",
                    BossTelegraphPattern.Rejection => "Deaths_Bliss",
                    BossTelegraphPattern.Invitation => "Deaths_Blight",
                    BossTelegraphPattern.Cross => "Cross",
                    BossTelegraphPattern.Divide => "Divide",
                    BossTelegraphPattern.Spread => "Severance",
                    BossTelegraphPattern.Stack => "Communion",
                    BossTelegraphPattern.BlueFirst or BossTelegraphPattern.PurpleFirst => "Constellation",
                    BossTelegraphPattern.BlackoutNorthWest or BossTelegraphPattern.BlackoutNorthEast or
                        BossTelegraphPattern.BlackoutSouthWest or BossTelegraphPattern.BlackoutSouthEast => "Vigil",
                    _ => null
                };
                if (thematicSuffix != null)
                    Assert.That(bar.CastName, Is.EqualTo(Loc.GetString("Revenant_Boss_" + thematicSuffix)), command);
                Assert.That(server.System<BossSystem>().CancelBossCast(boss), Is.True);
            }

            host.LocalShell.ExecuteCommand("crescentreap RevenantBoss sinistra,dextra,sinistra,dextra fast");
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.Queue, Is.EqualTo(new[] { L, R, L, R }));
            Assert.That(sequence.Interval, Is.EqualTo(2f));
            host.LocalShell.ExecuteCommand("crescentreap RevenantBoss cancel");
            Assert.That(server.EntMan.HasComponent<BossSequenceComponent>(boss), Is.False);
            foreach (var command in new[] { "crescentreap RevenantBoss", "bosssequence RevenantBoss" })
            {
                host.LocalShell.ExecuteCommand(command);
                Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).Queue.Count, Is.InRange(2, 4));
                host.LocalShell.ExecuteCommand("crescentreap RevenantBoss cancel");
            }
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task FourPreloadsReleaseAndSpacedPreviewImpactsUseOneFrozenQueue()
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        var queue = new[] { L, R, BossTelegraphPattern.Frons, BossTelegraphPattern.Tergum };
        await server.WaitAssertion(() =>
        {
            var attack = Configured(server, boss);
            Assert.That(attack.PreloadTime, Is.EqualTo(5));
            Assert.That(attack.ReleaseTime, Is.EqualTo(7));
            Assert.That(attack.StrikeInterval, Is.EqualTo(4));
            Assert.That(attack.WarningLeadTime, Is.EqualTo(2f));
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss, attack, true, queue), Is.True);
        });
        for (var i = 0; i < 4; i++)
        {
            var index = i;
            await server.WaitAssertion(() =>
            {
                var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
                Assert.That(sequence.Phase, Is.EqualTo(BossSequencePhase.Preparing));
                Assert.That(sequence.Index, Is.EqualTo(index));
                Assert.That(sequence.StrikesResolved, Is.Zero);
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.Preload, Is.True);
                Assert.That(warning.StoredStrike, Is.False);
                Assert.That(warning.ShowDanger(sequence.PhaseStart - TimeSpan.FromMilliseconds(1)), Is.False);
                Assert.That(warning.ShowDanger(sequence.PhaseStart), Is.True);
                Assert.That(warning.ShowDanger(sequence.PhaseStart + TimeSpan.FromSeconds(1.5)), Is.True);
                Assert.That(warning.ShowDanger(sequence.Deadline - TimeSpan.FromMilliseconds(1)), Is.True,
                    "Each preparation previews its full footprint for the whole cast without an impact.");
                Assert.That(warning.ShowDanger(sequence.Deadline), Is.False);
                Assert.That(warning.Pattern, Is.EqualTo(queue[index]));
                Assert.That(warning.Danger, Is.EquivalentTo(BossTilePatterns.Danger(queue[index], [], facing: sequence.Facing)));
                Assert.That(warning.Danger.Count, Is.EqualTo(66));
                foreach (var player in players)
                    Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
                Assert.That(bar.EndTime - bar.StartTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(bar.Visible, Is.True);
                Assert.That(bar.CastName, Is.EqualTo(Loc.GetString(
                    server.System<BossMechanicSystem>().CastNameKey(boss, queue[index], sentencing: true))));
                Assert.That(bar.CastName, Does.Not.Contain("/"));
            });
            await Advance(server, boss);
        }
        await server.WaitAssertion(() =>
        {
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.Phase, Is.EqualTo(BossSequencePhase.Releasing));
            Assert.That(sequence.StrikesResolved, Is.Zero);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName,
                Is.EqualTo(Loc.GetString("Revenant_Boss_Requiem")));
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Visible, Is.True);
            Assert.That(sequence.Deadline - sequence.PhaseStart, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Preload, Is.False);
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
            Assert.That(warning.ShowDanger(sequence.PhaseStart), Is.False);
            Assert.That(warning.ShowDanger(warning.DangerRevealTime - TimeSpan.FromMilliseconds(1)), Is.False);
            Assert.That(warning.ShowDanger(warning.DangerRevealTime), Is.True);
            Assert.That(warning.ShowDanger(warning.EndTime), Is.False);
        });
        await Advance(server, boss);
        TimeSpan previous = default;
        for (var hit = 1; hit <= 3; hit++)
        {
            var count = hit;
            await server.WaitAssertion(() =>
            {
                var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
                Assert.That(sequence.Phase, Is.EqualTo(BossSequencePhase.Resolving));
                Assert.That(sequence.StrikesResolved, Is.EqualTo(count));
                Assert.That(sequence.Queue, Is.EqualTo(queue));
                var bar = server.EntMan.GetComponent<BossCastBarComponent>(boss);
                Assert.That(bar.Casting, Is.True, "Internal deadlines still schedule the stored hits.");
                Assert.That(bar.Visible, Is.False);
                Assert.That(bar.CastName, Is.Empty);
                if (count > 1)
                    Assert.That((sequence.LastImpact - previous).TotalSeconds, Is.InRange(4, 4.1));
                previous = sequence.LastImpact;
                Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Pattern,
                    Is.EqualTo(queue[count]));
                var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
                Assert.That(warning.ShowDanger(sequence.PhaseStart), Is.False, "The next stored footprint stays hidden during repositioning.");
                Assert.That((warning.EndTime - warning.DangerRevealTime).TotalSeconds, Is.EqualTo(2));
                Assert.That(warning.ShowDanger(warning.EndTime - TimeSpan.FromSeconds(0.1)), Is.True);
                Assert.That(server.System<BossSystem>().TryStartBossCast(boss,
                    new BossTargetedAttack { CastTime = 1 }, [], false), Is.False);
            });
            await Advance(server, boss);
        }
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossSequenceComponent>(boss), Is.False);
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
            Assert.That(server.EntMan.GetComponent<BossSequenceHistoryComponent>(boss).Last, Is.EqualTo(queue));
            Assert.That(Configured(server, boss).NextAttack, Is.EqualTo(TimeSpan.Zero), "Preview cannot consume the real attack cooldown.");
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task LiveHitsUseCurrentTilesRespectArmorAndCooldownStartsAfterFinalHit()
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        var attack = new BossPreparedSequenceAttack { PreloadTime = 0.1f, ReleaseTime = 0.1f, Damage = Slash(10), Cooldown = 8 };
        await server.WaitAssertion(() =>
        {

            foreach (var player in players) server.EntMan.RemoveComponent<PassiveDamageComponent>(player);
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);

            server.EntMan.GetComponent<BossComponent>(boss).Stages[1].Attacks.Clear();
            foreach (var player in players) Move(server, arena, player, -2, 0);
            var armor = server.EntMan.SpawnEntity("MedievalClothingOuterArmorIron", new EntityCoordinates(arena.Comp.CombatGrid, -2.5f, 0.5f));
            Assert.That(server.System<InventorySystem>().TryEquip(players[1], armor, "outerClothing", force: true), Is.True);
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss, attack, queue: [L, R]), Is.True);
        });
        await Advance(server, boss);
        await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(attack.NextAttack, Is.EqualTo(TimeSpan.Zero));
        });
        await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            var unarmored = server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float();
            var armored = server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float();
            Assert.That(unarmored, Is.EqualTo(10));
            Assert.That(armored, Is.InRange(0.01f, 9.99f));
            Assert.That(attack.NextAttack, Is.EqualTo(TimeSpan.Zero));
            Move(server, arena, players[0], 2, 0);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Visible, Is.False);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).CastName, Is.Empty);
        });
        await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[0]).TotalDamage.Float(), Is.EqualTo(20));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(players[1]).TotalDamage.Float(), Is.LessThan(10));
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That((attack.NextAttack - now).TotalSeconds, Is.InRange(7.9, 8));
            Assert.That((server.EntMan.GetComponent<BossComponent>(boss).NextAttack - now).TotalSeconds, Is.InRange(2.9, 3));
        });
    }

    [TestCase(1.6f)]
    [TestCase(2f)]
    [TestCase(3f)]
    public async Task LateUpdateCannotCatchUpSeveralHitsOrShortenTheNextWindow(float interval)
    {
        var (server, arena, _) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() => Assert.That(server.System<BossSequenceSystem>().TryStart(boss,
            new BossPreparedSequenceAttack { PreloadTime = 0.1f, ReleaseTime = 0.1f }, true, [L, R, L, R], interval), Is.True));
        for (var i = 0; i < 5; i++) await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.StrikesResolved, Is.EqualTo(1));
            sequence.Deadline = server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(30);
            server.System<BossSequenceSystem>().Update(30);
            Assert.That(sequence.StrikesResolved, Is.EqualTo(2));
            Assert.That((sequence.Deadline - sequence.LastImpact).TotalSeconds, Is.EqualTo(interval).Within(0.001));
            server.System<BossSequenceSystem>().Update(0);
            Assert.That(sequence.StrikesResolved, Is.EqualTo(2));
        });
    }

    [TestCase(BossSequencePhase.Preparing)]
    [TestCase(BossSequencePhase.Releasing)]
    [TestCase(BossSequencePhase.Resolving)]
    public async Task CancellationInEachPhaseRemovesQueueAndWarningsButRetainsTank(BossSequencePhase phase)
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(boss, players[0]), Is.True);
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss,
                new BossPreparedSequenceAttack { PreloadTime = 0.1f, ReleaseTime = 0.1f }, true, [L, R]), Is.True);
        });
        if (phase != BossSequencePhase.Preparing)
            for (var i = 0; i < 2; i++) await Advance(server, boss);
        if (phase == BossSequencePhase.Resolving) await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).Phase, Is.EqualTo(phase));
            Assert.That(server.System<BossSystem>().CancelBossCast(boss), Is.True);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>().Where(effect => !effect.Comp.Impact), Is.Empty);
            Assert.That(server.EntMan.GetComponent<BossCastBarComponent>(boss).Casting, Is.False);
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[0]));
        });
        await RunSeconds(server, 4);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossSequenceComponent>(boss), Is.False);
            Assert.That(server.EntMan.AllEntities<BossTelegraphComponent>(), Is.Empty,
                "An already resolved visual may finish its fade, then must despawn.");
            foreach (var player in players)
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task PauseFreezesQueueAndWarningAndFocusTransferDoesNotChangeFacing()
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        TimeSpan remaining = default;
        int facing = 0;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossFocusMarkerSystem>().SetTank(boss, players[0]), Is.True);
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss, Configured(server, boss), true, [L, R, L]), Is.True);
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            facing = sequence.Facing;
            remaining = sequence.Deadline - server.ResolveDependency<IGameTiming>().CurTime;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await RunSeconds(server, 8);
        await server.WaitAssertion(() =>
        {
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.Index, Is.Zero);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            Assert.That((sequence.Deadline - server.ResolveDependency<IGameTiming>().CurTime).TotalSeconds,
                Is.EqualTo(remaining.TotalSeconds).Within(0.05));
            server.System<MobStateSystem>().ChangeMobState(players[0], MobState.Dead);
        });
        await server.WaitRunTicks(2);
        await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BossFocusMarkerComponent>(boss).Tank, Is.EqualTo(players[1]));
            Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).Facing, Is.EqualTo(facing));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Facing, Is.EqualTo(facing));
            Assert.That(server.EntMan.GetComponent<BossSequenceComponent>(boss).Queue, Is.EqualTo(new[] { L, R, L }));
        });
    }

    [TestCase("defeat")]
    [TestCase("wipe")]
    [TestCase("departure")]
    [TestCase("map")]
    public async Task EncounterEndOrArenaLossPreventsDelayedDamage(string reason)
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            var bosses = server.System<BossSystem>();
            Assert.That(bosses.TryStartBossfight(players.ToList(), boss), Is.True);
            server.EntMan.GetComponent<BossComponent>(boss).Stages[1].Attacks.Clear();
            Assert.That(server.System<BossSequenceSystem>().TryStart(boss,
                new BossPreparedSequenceAttack { Damage = Slash(10) }, queue: [L, R]), Is.True);
            switch (reason)
            {
                case "defeat": bosses.BossDefeated(boss, server.EntMan.GetComponent<BossComponent>(boss)); break;
                case "wipe": foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead); break;
                case "departure": foreach (var player in players) server.System<SharedTransformSystem>().SetCoordinates(player, new EntityCoordinates(arena.Owner, 100, 100)); break;
                case "map": server.EntMan.DeleteEntity(arena.Owner); break;
            }
        });
        await server.WaitRunTicks(12);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss), Is.Null);
            Assert.That(server.EntMan.AllEntities<BossSequenceComponent>(), Is.Empty);
            foreach (var player in players.Where(server.EntMan.EntityExists))
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(player).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task RandomRecipeSelectionSupportsAllLengthsAndRejectsInvalidOrRepeatingQueues()
    {
        var (server, arena, _) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            var system = server.System<BossSequenceSystem>();
            var attack = Configured(server, boss);
            foreach (var length in new[] { 2, 3, 4 })
            {
                attack.LengthWeights = new() { [length] = 1 };
                for (var iteration = 0; iteration < 12; iteration++)
                {
                    var history = server.EntMan.EnsureComponent<BossSequenceHistoryComponent>(boss);
                    var previous = history.Last.ToArray();
                    Assert.That(system.TryStart(boss, attack, true), Is.True);
                    var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
                    Assert.That(sequence.Queue.Count, Is.EqualTo(length));
                    Assert.That(sequence.Queue.SequenceEqual(previous), Is.False);
                    Assert.That(sequence.Queue.All(BossSequenceSystem.IsCleave), Is.True);
                    history.Last = sequence.Queue.ToList();
                    system.Cancel(boss);
                }
            }
            foreach (var invalid in new[] { new[] { L }, new[] { L, R, L, R, L }, new[] { L, BossTelegraphPattern.Spread } })
                Assert.That(system.TryStart(boss, attack, true, invalid), Is.False);
            foreach (var gap in new[] { 1f, 10.1f, float.NaN, float.PositiveInfinity })
                Assert.That(system.TryStart(boss, attack, true, [L, R], gap), Is.False);
            Assert.That(system.TryStart(boss, attack, queue: [L, R]), Is.False, "Damage requires an active encounter.");
            Assert.That(server.System<BossSystem>().TryStartBossCast(boss, new BossTargetedAttack { CastTime = 1 }, [], false), Is.True);
            Assert.That(system.TryStart(boss, attack, true, [L, R]), Is.False, "An ordinary cast already owns the boss.");
        });
    }

    [Test]
    public async Task StageSchedulerStartsConfiguredComboAndStaysLockedBetweenImpacts()
    {
        var (server, arena, players) = await Setup();
        var boss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BossSystem>().TryStartBossfight(players.ToList(), boss), Is.True);
            server.EntMan.GetComponent<BossEncounterComponent>(boss).FightRunning = false;
            server.EntMan.GetComponent<BossComponent>(boss).NextAttack = TimeSpan.Zero;
            Configured(server, boss).Damage = Slash(1);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.Preview, Is.False);
            Assert.That(sequence.Attack.NextAttack, Is.EqualTo(TimeSpan.Zero));
            Assert.That(sequence.Queue.Count, Is.InRange(2, 4));
        });
        var count = 0;
        await server.WaitAssertion(() => count = server.EntMan.GetComponent<BossSequenceComponent>(boss).Queue.Count);
        for (var i = 0; i <= count; i++) await Advance(server, boss);
        await server.WaitAssertion(() =>
        {
            var sequence = server.EntMan.GetComponent<BossSequenceComponent>(boss);
            Assert.That(sequence.StrikesResolved, Is.EqualTo(1));
            var other = new BossCastTest.CountingAction();
            var encounter = server.EntMan.GetComponent<BossComponent>(boss);
            encounter.Stages[1].Attacks.Add(new BossTargetedAttack { Actions = [other], Priority = 1000 });
            encounter.NextAttack = TimeSpan.Zero;
            server.System<BossSystem>().Update(0);
            Assert.That(other.Executions, Is.Zero, "Stage selection must stay blocked after the final cast bar finishes.");
            Assert.That(sequence.StrikesResolved, Is.EqualTo(1));
        });
    }

    private static BossPreparedSequenceAttack Configured(ServerIntegrationInstance server, EntityUid boss)
        => server.EntMan.GetComponent<BossComponent>(boss).Stages[1].Attacks.OfType<BossPreparedSequenceAttack>().Single();
    private static DamageSpecifier Slash(int amount) => new() { DamageDict = { ["Slash"] = FixedPoint2.New(amount) } };
    private static void Move(ServerIntegrationInstance server, Entity<BossArenaMapComponent> arena, EntityUid player, int x, int y)
        => server.System<SharedTransformSystem>().SetCoordinates(player, new EntityCoordinates(arena.Comp.CombatGrid, x + 0.5f, y + 0.5f));
    private static async Task Advance(ServerIntegrationInstance server, EntityUid boss)
    {
        double seconds = 0;
        await server.WaitAssertion(() =>
        {
            var timing = server.ResolveDependency<IGameTiming>();

            seconds = Math.Max(0, (server.EntMan.GetComponent<BossSequenceComponent>(boss).Deadline - timing.CurTime).TotalSeconds)
                + timing.TickPeriod.TotalSeconds * 2;
        });
        await RunSeconds(server, seconds);
    }
    private static Task RunSeconds(ServerIntegrationInstance server, double seconds)
        => server.WaitRunTicks((int)Math.Ceiling(seconds / server.ResolveDependency<IGameTiming>().TickPeriod.TotalSeconds));
    private async Task<(ServerIntegrationInstance Server, Entity<BossArenaMapComponent> Arena, EntityUid[] Players)> Setup()
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
        var players = new EntityUid[2];
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<MapLoaderSystem>().TryLoadMap(new ResPath("/Maps/Imperial/Medieval/revenant-arena.yml"),
                out var map, out _, new DeserializationOptions { InitializeMaps = true }), Is.True);
            arena = new(map!.Value.Owner, server.EntMan.GetComponent<BossArenaMapComponent>(map.Value.Owner));
            server.System<SharedMapSystem>().SetPaused(map.Value.Comp.MapId, false);
            if (!server.EntMan.EntityExists(arena.Comp.Boss))
            {
                if (arena.Comp.Crystal is { } crystal) server.EntMan.DeleteEntity(crystal);
                arena.Comp.Crystal = null;
                arena.Comp.Boss = server.EntMan.SpawnEntity("RevenantBoss", new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            }

            var encounter = server.EntMan.GetComponent<BossEncounterComponent>(arena.Comp.Boss);
            encounter.AutoStart = false;
            encounter.OpeningDelay = encounter.Recovery = 3600;
            encounter.TankDamage = new DamageSpecifier();
            for (var i = 0; i < players.Length; i++)
            {
                players[i] = server.EntMan.SpawnEntity("MobHuman", new EntityCoordinates(arena.Comp.CombatGrid, i * 4 - 1.5f, 0.5f));
                server.EntMan.RemoveComponent<ShieldOnStartupComponent>(players[i]);
                server.EntMan.EnsureComponent<BossArenaPartyMemberComponent>(players[i]);
            }
        });
        return (server, arena, players);
    }
}
