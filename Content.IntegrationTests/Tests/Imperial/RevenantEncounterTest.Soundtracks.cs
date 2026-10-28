using System.Collections.Generic;
using Robust.UnitTesting;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Audio.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task DefaultDestinationBeforeCrystalBreakUsesIntroLoopFinaleAndSurvivesReset()
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            LoadAlternateTestSoundtrack(server);
            var entities = server.EntMan;
            var system = server.System<BossMusicSystem>();
            var timing = server.ResolveDependency<IGameTiming>();
            if (entities.TryGetComponent<BossArenaMusicComponent>(arena.Owner, out var idleMusic))
            {
                Assert.That(idleMusic.Phase, Is.EqualTo(BossMusicPhase.Stopped));
                Assert.That(idleMusic.Stream, Is.Null);
            }
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Crystal!.Value, Hit(100), origin: players[1]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            Assert.That(music.SelectedSoundtrack, Is.Null, "Destination is the default without a command override.");
            Assert.That(music.ActiveSoundtrack, Is.EqualTo(new ProtoId<BossSoundtrackPrototype>("destination")));
            var boss = arena.Comp.Boss;
            var intro = music.Stream!.Value;
            var audio = entities.GetComponent<AudioComponent>(intro);
            Assert.That(audio.FileName, Does.EndWith("/Destination/intro.wav"));
            Assert.That(audio.Global, Is.True);
            Assert.That(audio.Params.Loop, Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(intro).ParentUid, Is.EqualTo(arena.Owner));
            Assert.That((music.TransitionAt!.Value - timing.CurTime).TotalSeconds, Is.EqualTo(159.9156875).Within(0.00001));
            music.TransitionAt = timing.CurTime;
            system.Update(0);
            var loop = music.Stream!.Value;
            audio = entities.GetComponent<AudioComponent>(loop);
            Assert.That(audio.FileName, Does.EndWith("/Destination/loop.wav"));
            Assert.That(audio.Params.Loop, Is.True);
            Assert.That(music.LoopLength.TotalSeconds, Is.EqualTo(147.2566042).Within(0.00001));
            var loopAudioStart = audio.AudioStart;
            music.LoopStartedAt = timing.CurTime - music.LoopLength * 4 - TimeSpan.FromSeconds(2);
            music.TransitionAt = timing.CurTime;
            system.Update(0);
            Assert.That(music.Stream, Is.EqualTo(loop));
            Assert.That(audio.AudioStart, Is.EqualTo(loopAudioStart), "The server cannot seek an already looping source.");
            Assert.That(server.System<BossEncounterSystem>().Reset(boss, arena), Is.True);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Stopped));
            server.System<SharedTransformSystem>().SetCoordinates(players[1], new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            server.System<BossArenaMapSystem>().RecordEntry(players[1], gate, arena.Comp.Entrance);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Crystal!.Value, Hit(100), origin: players[1]);
            Assert.That(entities.GetComponent<AudioComponent>(music.Stream!.Value).FileName, Does.EndWith("/Destination/intro.wav"));
            boss = arena.Comp.Boss;
            server.System<BossSystem>().DamageBoss(boss, 100000);
            audio = entities.GetComponent<AudioComponent>(music.Stream!.Value);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Outro));
            Assert.That(audio.FileName, Does.EndWith("/Destination/outro.wav"));
            Assert.That(audio.Params.Loop, Is.False);
            Assert.That((music.TransitionAt!.Value - timing.CurTime).TotalSeconds, Is.EqualTo(10.3191875).Within(0.00001));
        });
    }

    [TestCase(false)] [TestCase(true)]
    public async Task SoundtrackCommandKeepsCurrentCombatPhaseStopsOldTrackAndUsesSelectedFinale(bool looping)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadAlternateTestSoundtrack(server);
            var entities = server.EntMan;
            Assert.That(server.System<BossMusicSystem>().TrySetSoundtrack(arena.Owner, "test-alternate-destination"), Is.True);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            if (looping)
            {
                music.TransitionAt = server.ResolveDependency<IGameTiming>().CurTime;
                server.System<BossMusicSystem>().Update(0);
            }
            var console = server.ResolveDependency<IConsoleHost>().LocalShell;
            var old = music.Stream!.Value;
            console.ExecuteCommand("bossmusic destination");
            Assert.That(entities.IsQueuedForDeletion(old), Is.True);
            var selected = music.Stream!.Value;
            Assert.That(music.Phase, Is.EqualTo(looping ? BossMusicPhase.Loop : BossMusicPhase.Intro));
            Assert.That(entities.GetComponent<AudioComponent>(selected).FileName,
                Does.EndWith(looping ? "/Destination/loop.wav" : "/Destination/intro.wav"));
            console.ExecuteCommand("bossmusic destination");
            Assert.That(music.Stream, Is.EqualTo(selected), "Selecting the same version cannot restart it.");
            Assert.That(server.System<BossMusicSystem>().TrySetSoundtrack(arena.Owner, "missing"), Is.False);
            Assert.That(music.Stream, Is.EqualTo(selected), "Invalid versions cannot interrupt the current track.");
            Assert.That(server.System<BossMusicSystem>().TrySetSoundtrack(players[0], "destination"), Is.False);
            console.ExecuteCommand("bossmusic test-alternate-destination");
            Assert.That(entities.GetComponent<AudioComponent>(music.Stream!.Value).FileName,
                Does.EndWith(looping ? "/RevenantBoss/Destination/loop.wav" : "/RevenantBoss/Destination/intro.wav"));
            console.ExecuteCommand("bossmusic destination");
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, 100000);
            Assert.That(entities.GetComponent<AudioComponent>(music.Stream!.Value).FileName, Does.EndWith("/Destination/outro.wav"));
        });
    }

    [Test]
    public async Task SoundtrackCanSwitchVictoryFinaleAfterBossDespawnAndStopsAtItsEnd()
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            LoadAlternateTestSoundtrack(server);
            Assert.That(server.System<BossMusicSystem>().TrySetSoundtrack(arena.Owner, "test-alternate-destination"), Is.True);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Boss, Hit(1), origin: players[0]);
            server.System<BossSystem>().DamageBoss(arena.Comp.Boss, 100000);
        });
        await server.WaitRunTicks(135);
        await server.WaitAssertion(() =>
        {
            LoadAlternateTestSoundtrack(server);
            var entities = server.EntMan;
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            var music = entities.GetComponent<BossArenaMusicComponent>(arena.Owner);
            var previous = music.Stream!.Value;
            server.ResolveDependency<IConsoleHost>().LocalShell.ExecuteCommand("bossmusic destination");
            Assert.That(entities.IsQueuedForDeletion(previous), Is.True);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Outro));
            Assert.That(entities.GetComponent<AudioComponent>(music.Stream!.Value).FileName, Does.EndWith("/Destination/outro.wav"));
            music.TransitionAt = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMusicSystem>().Update(0);
            Assert.That(music.Phase, Is.EqualTo(BossMusicPhase.Stopped));
            Assert.That(music.Stream, Is.Null);
        });
    }

    private static void LoadAlternateTestSoundtrack(ServerIntegrationInstance server)
    {
        var manager = server.ResolveDependency<IPrototypeManager>();
        if (manager.TryIndex<BossSoundtrackPrototype>("test-alternate-destination", out _)) return;
        var changed = new Dictionary<Type, HashSet<string>>();
        manager.LoadString("""
            - type: bossSoundtrack
              id: test-alternate-destination
              name: Boss_Soundtrack_Destination
              intro: /Audio/Imperial/Medieval/RevenantBoss/Destination/intro.wav
              loop: /Audio/Imperial/Medieval/RevenantBoss/Destination/loop.wav
              outro: /Audio/Imperial/Medieval/RevenantBoss/Destination/outro.wav
            """, changed: changed);
        manager.ReloadPrototypes(changed);
    }

}
