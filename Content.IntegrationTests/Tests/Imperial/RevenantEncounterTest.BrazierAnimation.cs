using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [Test]
    public async Task BraziersRetainIndependentFlamePhasesThroughStateChanges()
    {
        var (server, arena, _, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var mechanics = server.System<BossMechanicSystem>();
            mechanics.EnsureBraziers(arena);
            var entities = server.EntMan;
            var animations = arena.Comp.Braziers.Select(uid => entities.GetComponent<BossBrazierAnimationComponent>(uid)).ToArray();
            Assert.That(animations.Select(a => a.PhaseOffsetFrames), Is.EqualTo(new[] { 0, 7, 15, 24 }));
            var starts = animations.Select(a => a.StartTime).ToArray();
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            var before = animations.Select(a => BossBrazierAnimation.TimeAt(a, now)).ToArray();
            Assert.That(before.Distinct().Count(), Is.EqualTo(4));
            foreach (var state in new[] { BossBrazierState.White, BossBrazierState.Purple, BossBrazierState.Victory, BossBrazierState.Empty })
            {
                mechanics.SetBraziersState(arena, state);
                Assert.That(animations.Select(a => a.StartTime), Is.EqualTo(starts));
                Assert.That(animations.Select(a => BossBrazierAnimation.TimeAt(a, now)), Is.EqualTo(before));
            }
            foreach (var animation in animations)
                Assert.That(BossBrazierAnimation.TimeAt(animation, now + TimeSpan.FromSeconds(6.4)),
                    Is.EqualTo(BossBrazierAnimation.TimeAt(animation, now)).Within(.00001));
        });
    }
}
