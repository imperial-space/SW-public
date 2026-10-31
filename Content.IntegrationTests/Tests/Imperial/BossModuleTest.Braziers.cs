using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task BraziersStayAtTheirSafeSquareCentersWithoutVigil()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            Assert.That(arena.Comp.Braziers, Has.Count.EqualTo(4));
            for (var i = 0; i < 4; i++)
            {
                var pattern = (BossTelegraphPattern)((int)BossTelegraphPattern.BlackoutNorthWest + i);
                var tile = BossTilePatterns.BlackoutCenter(pattern, arena.Comp.HalfSize);
                var position = server.EntMan.GetComponent<TransformComponent>(arena.Comp.Braziers[i]).Coordinates.Position;
                Assert.That(position.X, Is.EqualTo(tile.X + .5f));
                Assert.That(position.Y, Is.EqualTo(tile.Y + .5f));
                Assert.That(server.EntMan.HasComponent<BossBrazierAnimationComponent>(arena.Comp.Braziers[i]), Is.True);
            }
        });
    }
}
