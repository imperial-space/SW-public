using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task FirstHitStartsArenaIntroOnceAndNativeLoopNeverSeeksOrReplacesTheStream()
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var timing = server.ResolveDependency<IGameTiming>();
            entities.GetComponent<BossMusicComponent>(boss).Soundtrack = "destination";
            Assert.That(entities.HasComponent<BossArenaMusicComponent>(arena.Owner), Is.False);
            server.System<DamageableSystem>().TryChangeDamage(boss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            var intro = music.Stream!.Value;
            var audio = entities.GetComponent<AudioComponent>(intro);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Intro));
            Assert.That(audio.FileName, Does.EndWith("/RevenantBoss/Destination/intro.wav"));
            Assert.That(audio.Global, Is.True, "Map music has no positional falloff.");
            Assert.That(entities.GetComponent<TransformComponent>(intro).MapUid, Is.EqualTo(arena.Owner),
                "A non-nullspace map restricts hearing to this arena in the native client.");
            Assert.That(entities.GetComponent<TransformComponent>(intro).ParentUid, Is.EqualTo(arena.Owner));
            Assert.That(audio.IncludedEntities, Is.Null, "Late arrivals are not locked out by a snapshot of recipients.");
            Assert.That(audio.Params.Loop, Is.False);
            Assert.That((music.TransitionAt!.Value - timing.CurTime).TotalSeconds, Is.EqualTo(159.9156875).Within(0.000001));
            server.System<DamageableSystem>().TryChangeDamage(boss, Hit(1), origin: players[1]);
            server.System<BossSystem>().Update(0);
            Assert.That(music.Stream, Is.EqualTo(intro), "Further hits cannot restart the introduction.");
            Assert.That(entities.GetComponent<BossComponent>(boss).SongEntity, Is.Null, "The legacy song scheduler must not overlap this playlist.");
            var transforms = server.System<SharedTransformSystem>();
            transforms.SetCoordinates(players[0], new EntityCoordinates(entities.GetComponent<TransformComponent>(gate).GridUid!.Value, 0.5f, -0.5f));
            transforms.SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, -2.5f, -1.5f));
            server.System<BossArenaProtectionSystem>().MoveBoss(boss, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            Assert.That(music.Stream, Is.EqualTo(intro), "Moving listeners or relocating for Blackout never restarts the song.");
            Assert.That(entities.GetComponent<TransformComponent>(intro).MapUid, Is.EqualTo(arena.Owner));
            music.TransitionAt = timing.CurTime;
            server.System<BossMusicSystem>().Update(0);
            var loop = music.Stream!.Value;
            audio = entities.GetComponent<AudioComponent>(loop);
            Assert.That(loop, Is.Not.EqualTo(intro));
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Loop));
            Assert.That(audio.FileName, Does.EndWith("/RevenantBoss/Destination/loop.wav"));
            Assert.That(audio.Params.Loop, Is.True);
            Assert.That(entities.HasComponent<TimedDespawnComponent>(loop), Is.False);
            Assert.That(music.LoopLength.TotalSeconds, Is.EqualTo(147.2566042).Within(0.000001));
            server.System<AudioSystem>().SetPlaybackPosition(loop, (float)music.LoopLength.TotalSeconds - 1);
            var originalStart = audio.AudioStart;
            music.LoopStartedAt = timing.CurTime - music.LoopLength * 3 - TimeSpan.FromSeconds(2);
            music.TransitionAt = timing.CurTime;
            server.System<BossMusicSystem>().Update(0);
            Assert.That(music.Stream, Is.EqualTo(loop), "Native looping stays continuous across cycle boundaries.");
            Assert.That(audio.AudioStart, Is.EqualTo(originalStart));
        });
    }

    [Test]
    public async Task PausingTheArenaFreezesIntroAndAudioTogether()
    {
        var (server, arena, players, _) = await Setup();
        TimeSpan transition = default, audioStart = default;
        await server.WaitAssertion(() =>
        {
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            var music = server.EntMan.GetComponent<BossArenaMusicComponent>(arena.Owner);
            transition = music.TransitionAt!.Value;
            audioStart = server.EntMan.GetComponent<AudioComponent>(music.Stream!.Value).AudioStart;
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, true);
        });
        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var music = server.EntMan.GetComponent<BossArenaMusicComponent>(arena.Owner);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Intro));
            Assert.That(music.TransitionAt, Is.EqualTo(transition));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID, false);
            var shift = music.TransitionAt!.Value - transition;
            Assert.That(shift.TotalSeconds, Is.GreaterThan(1));
            Assert.That(server.EntMan.GetComponent<AudioComponent>(music.Stream!.Value).AudioStart - audioStart, Is.EqualTo(shift));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task VictoryReplacesIntroOrLoopWithOneOutroThatOutlivesBoss(bool finishIntro)
    {
        var (server, arena, players, _) = await Setup();
        EntityUid outro = default, oldBoss = arena.Comp.Boss;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var timing = server.ResolveDependency<IGameTiming>();
            entities.GetComponent<BossMusicComponent>(oldBoss).Soundtrack = "destination";
            server.System<DamageableSystem>().TryChangeDamage(oldBoss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            if (finishIntro)
            {
                music.TransitionAt = timing.CurTime;
                server.System<BossMusicSystem>().Update(0);
            }
            var combatTrack = music.Stream!.Value;
            server.System<BossSystem>().DamageBoss(oldBoss, server.EntMan.GetComponent<BossComponent>(oldBoss).MaxHealth * 2);
            outro = music.Stream!.Value;
            Assert.That(outro, Is.Not.EqualTo(combatTrack));
            Assert.That(entities.IsQueuedForDeletion(combatTrack), Is.True);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Outro));
            var audio = entities.GetComponent<AudioComponent>(outro);
            Assert.That(audio.FileName, Does.EndWith("/RevenantBoss/Destination/outro.wav"));
            Assert.That(audio.Params.Loop, Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(outro).ParentUid, Is.EqualTo(arena.Owner));
            Assert.That((music.TransitionAt!.Value - timing.CurTime).TotalSeconds, Is.EqualTo(10.3191875).Within(0.000001));
            server.System<BossSystem>().DamageBoss(oldBoss, 1);
            Assert.That(music.Stream, Is.EqualTo(outro), "Repeated defeat attempts cannot start another outro.");
        });
        await server.WaitRunTicks(135);
        await server.WaitAssertion(() =>
        {
            var music = server.EntMan.GetComponent<BossArenaMusicComponent>(arena.Owner);
            Assert.That(server.EntMan.EntityExists(oldBoss), Is.False);
            Assert.That(server.EntMan.EntityExists(outro), Is.True);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Outro));
            music.TransitionAt = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMusicSystem>().Update(0);
            Assert.That(music.Stream, Is.Null);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Stopped));
            Assert.That(server.EntMan.IsQueuedForDeletion(outro), Is.True);
        });
    }

    [Test]
    public async Task WipeResetStopsMusicWithoutOutroAndNewAttemptStartsItsIntro()
    {
        var (server, arena, players, gate) = await Setup();
        EntityUid intro = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var oldBoss = arena.Comp.Boss;
            server.System<DamageableSystem>().TryChangeDamage(oldBoss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            intro = music.Stream!.Value;
            foreach (var player in players) server.System<MobStateSystem>().ChangeMobState(player, MobState.Dead);
            var encounter = server.System<BossEncounterSystem>();
            encounter.Update(0);
            var rotation = entities.GetComponent<BossEncounterComponent>(oldBoss);
            rotation.IdleDeadline = server.ResolveDependency<IGameTiming>().CurTime;
            rotation.NextResetAttempt = rotation.IdleDeadline.Value;
            encounter.Update(0);
            Assert.That(arena.Comp.Boss, Is.Not.EqualTo(oldBoss));
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Stopped));
            Assert.That(music.Stream, Is.Null);
            Assert.That(entities.IsQueuedForDeletion(intro), Is.True);
            Assert.That(entities.AllEntities<AudioComponent>().Any(audio =>
            {
                var fileName = audio.Comp.FileName;
                return fileName.EndsWith("/RevenantBoss/Destination/outro.wav");
            }), Is.False);
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            server.System<BossArenaMapSystem>().RecordEntry(players[0], gate, arena.Comp.Entrance);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Crystal!.Value, Hit(100), origin: players[0]);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Intro));
        });
        await CrystalSeconds(server, 5.2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Intro));
            Assert.That(music.Boss, Is.EqualTo(arena.Comp.Boss));
            Assert.That(music.Stream, Is.Not.EqualTo(intro));
            Assert.That(entities.GetComponent<AudioComponent>(music.Stream!.Value).AudioStart,
                Is.LessThanOrEqualTo(server.ResolveDependency<IGameTiming>().CurTime));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ArenaOrBossDeletionCannotLeaveTheCombatStreamBehind(bool deleteMap)
    {
        var (server, arena, players, _) = await Setup();
        EntityUid stream = default;
        await server.WaitAssertion(() =>
        {
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            stream = server.EntMan.GetComponent<BossArenaMusicComponent>(arena.Owner).Stream!.Value;
            server.EntMan.DeleteEntity(deleteMap ? arena.Owner : arena.Comp.Boss);
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(stream), Is.False));
    }
}
