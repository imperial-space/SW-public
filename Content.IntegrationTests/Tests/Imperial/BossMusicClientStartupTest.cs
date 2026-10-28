using System.IO;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client;
using Robust.Shared.Analyzers;
using Robust.Shared.Analyzers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossMusicClientStartupTest
{
    [Test]
    public async Task BossMusicAndNativeAudioCanStartTogetherAndRestartForAnotherConnection()
    {
        using var client = new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart = false,
            LoadTestAssembly = false,
            Options = new GameControllerOptions
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                PrototypeDirectory = new("/BossMusicStartupNoWorld/"),
                MountOptions = new()
                {
                    DirMounts = { Path.GetFullPath("../../RobustToolbox/Resources", TestContext.CurrentContext.TestDirectory) }
                }
            },
            BeforeRegisterComponents = () => IoCManager.Resolve<IComponentFactory>().RegisterClass<BossMusicStreamComponent>(),
            BeforeStart = () => IoCManager.Resolve<IEntitySystemManager>().LoadExtraSystemType<BossMusicPlaybackSystem>()
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(() =>
        {
            Assert.DoesNotThrow(() => client.EntMan.Startup());
            Assert.That(client.System<Robust.Client.Audio.AudioSystem>(), Is.Not.Null);
            Assert.That(client.System<BossMusicPlaybackSystem>(), Is.Not.Null);
            var audioSystem = client.System<Robust.Client.Audio.AudioSystem>();
            var stream = audioSystem.PlayGlobal(new ResolvedPathSpecifier("/Audio/Imperial/Medieval/RevenantBoss/Destination/loop.wav"),
                Filter.Local(), false, AudioParams.Default.WithLoop(true))!.Value;
            var music = client.EntMan.EnsureComponent<BossMusicStreamComponent>(stream.Entity);
            var now = client.ResolveDependency<IGameTiming>().CurTime;
            music.LoopLength = audioSystem.GetAudioLength(new ResolvedPathSpecifier("/Audio/Imperial/Medieval/RevenantBoss/Destination/loop.wav"));
            music.StartTime = now - music.LoopLength * 4 - TimeSpan.FromSeconds(2);
            var playback = client.System<BossMusicPlaybackSystem>();
            playback.FrameUpdate(0);
            Assert.That(music.ClientStarted, Is.True);
            var audio = client.EntMan.GetComponent<AudioComponent>(stream.Entity);
            Assert.That((now - audio.AudioStart).TotalSeconds, Is.EqualTo(2).Within(.00001));
            var stableStart = audio.AudioStart;
            var state = new AfterAutoHandleStateEvent();
            client.EntMan.EventBus.RaiseComponentEvent(stream.Entity, music, ref state);
            Assert.That(music.ClientStarted, Is.False, "A newly configured clock needs one initial seek.");
            playback.FrameUpdate(0);
            client.EntMan.EventBus.RaiseComponentEvent(stream.Entity, music, ref state);
            Assert.That(music.ClientStarted, Is.True, "An unchanged full state must not reinitialize playback.");
            playback.FrameUpdate(0);
            Assert.That(audio.AudioStart, Is.EqualTo(stableStart), "Ordinary frames cannot seek the loop again.");
            var intro = audioSystem.PlayGlobal(new ResolvedPathSpecifier("/Audio/Imperial/Medieval/RevenantBoss/Destination/intro.wav"),
                Filter.Local(), false)!.Value;
            var introMusic = client.EntMan.EnsureComponent<BossMusicStreamComponent>(intro.Entity);
            introMusic.TrackLength = TimeSpan.FromSeconds(159.9156875);
            introMusic.StartTime = now - TimeSpan.FromSeconds(.25);
            playback.FrameUpdate(0);
            var introAudio = client.EntMan.GetComponent<AudioComponent>(intro.Entity);
            Assert.That((now - introAudio.AudioStart).TotalSeconds, Is.EqualTo(.25).Within(.00001),
                "The intro must use the same clock as the queued loop despite the native one-second startup grace.");
            var future = audioSystem.PlayGlobal(new ResolvedPathSpecifier("/Audio/Imperial/Medieval/RevenantBoss/Destination/loop.wav"),
                Filter.Local(), false, AudioParams.Default.WithLoop(true))!.Value;
            var futureMusic = client.EntMan.EnsureComponent<BossMusicStreamComponent>(future.Entity);
            futureMusic.LoopLength = music.LoopLength;
            futureMusic.StartTime = now + TimeSpan.FromSeconds(.5);
            playback.FrameUpdate(0);
            Assert.That(futureMusic.ClientStarted, Is.False);
            Assert.That(future.Component.Gain, Is.Zero, "A queued loop must remain inaudible before its deadline.");
            client.EntMan.Shutdown();
            client.ResolveDependency<IEntitySystemManager>().LoadExtraSystemType<BossMusicPlaybackSystem>();
            Assert.DoesNotThrow(() => client.EntMan.Startup());
            Assert.That(client.System<BossMusicPlaybackSystem>(), Is.Not.Null);
            client.EntMan.Shutdown();
        });
    }
}
