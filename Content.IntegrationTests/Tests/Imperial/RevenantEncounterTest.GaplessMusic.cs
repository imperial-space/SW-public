using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task IntroPreparesAnArenaLocalLoopAndPromotesThatExactStreamWithoutSeeking()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            var intro = music.Stream!.Value;
            var introCue = entities.GetComponent<BossMusicStreamComponent>(intro);
            Assert.That(introCue.PreloadTrack, Does.EndWith("/Destination/loop.wav"));
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            music.TransitionAt = now + TimeSpan.FromSeconds(.5);
            server.System<BossMusicSystem>().Update(0);
            var queued = music.PendingLoop!.Value;
            var cue = entities.GetComponent<BossMusicStreamComponent>(queued);
            var audio = entities.GetComponent<AudioComponent>(queued);
            var start = audio.AudioStart;
            Assert.That(music.Stream, Is.EqualTo(intro));
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Intro));
            Assert.That(cue.StartTime, Is.EqualTo(music.TransitionAt));
            Assert.That(cue.Position(now), Is.Zero);
            Assert.That(audio.Params.Loop, Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(queued).ParentUid, Is.EqualTo(arena.Owner));
            server.System<BossMusicSystem>().Update(0);
            Assert.That(music.PendingLoop, Is.EqualTo(queued), "Preparation cannot allocate duplicate streams.");
            music.TransitionAt = now;
            server.System<BossMusicSystem>().Update(0);
            Assert.That(music.Stream, Is.EqualTo(queued));
            Assert.That(music.PendingLoop, Is.Null);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Loop));
            Assert.That(audio.AudioStart, Is.EqualTo(start));
            Assert.That(entities.IsQueuedForDeletion(intro), Is.True);
            music.LoopStartedAt = now - cue.LoopLength * 20;
            music.TransitionAt = now;
            server.System<BossMusicSystem>().Update(0);
            Assert.That(audio.AudioStart, Is.EqualTo(start));
            Assert.That(music.Stream, Is.EqualTo(queued));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task VictoryOrSoundtrackSwitchCancelsThePreparedLoop(bool switchVersion)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            music.TransitionAt = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(.5);
            var system = server.System<BossMusicSystem>();
            system.Update(0);
            var queued = music.PendingLoop!.Value;
            if (switchVersion)
            {
                LoadAlternateTestSoundtrack(server);
                Assert.That(system.TrySetSoundtrack(arena.Owner, "test-alternate-destination"), Is.True);
            }
            else server.System<BossSystem>().DamageBoss(arena.Comp.Boss, 100000);
            Assert.That(music.PendingLoop, Is.Null);
            Assert.That(entities.IsQueuedForDeletion(queued), Is.True);
        });
    }

    [TestCase(-1, 0)] [TestCase(0, 0)] [TestCase(10, 0)]
    [TestCase(12, 2)] [TestCase(207, 7)]
    public void ClientMusicClockUsesModuloForLateArrivalsWithoutServerBoundaryUpdates(double elapsed, double expected)
    {
        var cue = new BossMusicStreamComponent { StartTime = TimeSpan.FromSeconds(100), LoopLength = TimeSpan.FromSeconds(10) };
        Assert.That(cue.Position(TimeSpan.FromSeconds(100 + elapsed)), Is.EqualTo(expected));
    }
}
