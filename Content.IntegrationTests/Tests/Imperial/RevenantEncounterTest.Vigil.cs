using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    private static void FinishVigilRecovery(ServerIntegrationInstance server, EntityUid boss)
    {
        var mechanic = server.EntMan.GetComponent<BossMechanicComponent>(boss);
        var visual = server.EntMan.GetComponent<BossVigilComponent>(boss);
        var now = server.ResolveDependency<IGameTiming>().CurTime;
        visual.ResolveTime = now - TimeSpan.FromSeconds(BossVigilVisuals.RecoverySeconds);
        mechanic.BlackoutReturnStarted = false;
        mechanic.Deadline = now;
        server.System<BossMechanicSystem>().Update(0);
    }

    [TestCase(BossTelegraphPattern.BlackoutNorthWest)]
    [TestCase(BossTelegraphPattern.BlackoutNorthEast)]
    [TestCase(BossTelegraphPattern.BlackoutSouthWest)]
    [TestCase(BossTelegraphPattern.BlackoutSouthEast)]
    public async Task VigilMarksOnlyUnsafeVictimsOnceAndHoldsRecoveryLock(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var original = entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor;
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss,new() { Telegraph=pattern, CastTime=6, Damage=Hit(1) }),Is.True);
            var safe = BossTilePatterns.BlackoutCenter(pattern);
            for (var i=0;i<players.Length;i++)
                server.System<SharedTransformSystem>().SetCoordinates(players[i],
                    new EntityCoordinates(arena.Comp.CombatGrid,i<2?safe.X+.5f:i+.5f,i<2?safe.Y+.5f:.5f));
            var warning = server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp;
            Assert.That(warning.ShowDanger(warning.EndTime-TimeSpan.FromMilliseconds(50)),Is.False);
            var mechanic = entities.GetComponent<BossMechanicComponent>(boss);
            mechanic.Deadline=server.ResolveDependency<IGameTiming>().CurTime;
            mechanics.Update(0);
            var visual=entities.GetComponent<BossVigilComponent>(boss);
            Assert.That(visual.Victims,Is.EquivalentTo(players.Skip(2)));
            Assert.That(visual.Resolved && mechanic.BlackoutRecovery,Is.True);
            Assert.That((mechanic.Deadline-visual.ResolveTime).TotalSeconds,Is.EqualTo(2.5));
            Assert.That(entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.EqualTo(Color.Black));
            Assert.That(mechanics.TryStart(boss,new() { Telegraph=BossTelegraphPattern.Cross,CastTime=6,Damage=Hit(1) }),Is.False);
            for(var i=0;i<players.Length;i++)
                Assert.That(entities.GetComponent<DamageableComponent>(players[i]).TotalDamage.Float(),Is.EqualTo(i<2?0:1));
            Assert.That(entities.AllEntities<MetaDataComponent>().Any(e=>e.Comp.EntityPrototype?.ID=="RevenantBossTileHitEffect"),Is.False);

            server.System<SharedTransformSystem>().SetCoordinates(players[2],
                new EntityCoordinates(arena.Comp.CombatGrid,safe.X+.5f,safe.Y+.5f));
            mechanics.Update(0);
            Assert.That(visual.Victims,Is.EquivalentTo(players.Skip(2)));
            Assert.That(entities.GetComponent<DamageableComponent>(players[2]).TotalDamage.Float(),Is.EqualTo(1));
            foreach(var brazier in arena.Comp.Braziers)
                Assert.That(entities.GetComponent<PointLightComponent>(brazier).Enabled,Is.True);
            FinishVigilRecovery(server,boss);
            Assert.That(entities.HasComponent<BossVigilComponent>(boss)||entities.HasComponent<BossMechanicComponent>(boss),Is.False);
            Assert.That(entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.EqualTo(original));
            Assert.That(entities.GetComponent<PointLightComponent>(boss).Energy,Is.EqualTo(3));
            Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss),Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task VigilCancellationRestoresNativeLightsDuringEveryPhase(int phase)
    {
        var (server,arena,players,_)=await Setup();
        await server.WaitAssertion(() =>
        {
            var entities=server.EntMan;
            var boss=arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(),boss);
            var original=entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor;
            var mechanics=server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss,new() { Telegraph=BossTelegraphPattern.BlackoutSouthEast,CastTime=6,Damage=Hit(1) }),Is.True);
            if(phase>0)
            {
                var mechanic=entities.GetComponent<BossMechanicComponent>(boss);
                mechanic.Deadline=server.ResolveDependency<IGameTiming>().CurTime;
                mechanics.Update(0);
                if(phase==2)
                {
                    var visual=entities.GetComponent<BossVigilComponent>(boss);
                    visual.ResolveTime=server.ResolveDependency<IGameTiming>().CurTime-TimeSpan.FromSeconds(1.75);
                    mechanics.Update(0);
                    Assert.That(entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.Not.EqualTo(Color.Black));
                }
            }
            Assert.That(server.System<BossSystem>().CancelBossCast(boss),Is.True);
            Assert.That(entities.HasComponent<BossVigilComponent>(boss),Is.False);
            Assert.That(entities.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.EqualTo(original));
            Assert.That(entities.GetComponent<BossPresentationComponent>(boss).Hidden,Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates.Position,Is.EqualTo(new System.Numerics.Vector2(.5f,.5f)));
            foreach(var brazier in arena.Comp.Braziers) Assert.That(entities.GetComponent<PointLightComponent>(brazier).Enabled,Is.False);
            Assert.That(entities.AllEntities<BossTelegraphComponent>().Any(e=>e.Comp.Boss==boss),Is.False);
        });
    }

    [TestCase(BossTelegraphPattern.BlackoutNorthWest)]
    [TestCase(BossTelegraphPattern.BlackoutNorthEast)]
    [TestCase(BossTelegraphPattern.BlackoutSouthWest)]
    [TestCase(BossTelegraphPattern.BlackoutSouthEast)]
    public async Task VigilReturnsToCenterBeforeDelayedSmoothBodyFade(BossTelegraphPattern pattern)
    {
        var (server, arena, players, _) = await Setup();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            server.System<BossSystem>().StartBossfight(players.ToList(), boss);
            var mechanics = server.System<BossMechanicSystem>();
            Assert.That(mechanics.TryStart(boss, new() { Telegraph = pattern, CastTime = 6, Damage = Hit(1) }), Is.True);
            var mechanic = entities.GetComponent<BossMechanicComponent>(boss);
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            mechanic.BlackoutMoveTime = now;
            mechanics.Update(0);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates.Position,
                Is.Not.EqualTo(new System.Numerics.Vector2(.5f, .5f)));
            mechanic.Deadline = now;
            mechanics.Update(0);
            var visual = entities.GetComponent<BossVigilComponent>(boss);
            visual.StartTime = now - TimeSpan.FromSeconds(6);
            visual.ResolveTime = now - TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds + .2);
            mechanics.Update(0);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates,
                Is.EqualTo(new EntityCoordinates(arena.Comp.CombatGrid, .5f, .5f)), "Relocate while fully hidden, before any returning boss pixels.");
            Assert.That(BossVigilVisuals.ReturnProgress(visual, now), Is.GreaterThan(0), "Darkness starts clearing first.");
            Assert.That(BossVigilVisuals.BodyOpacity(visual, now), Is.Zero);
            Assert.That(BossVigilVisuals.AshFrame(visual, now), Is.EqualTo(-1), "Scattered ash is not played backward during reappearance.");
            visual.ResolveTime = now - TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds + .39);
            Assert.That(BossVigilVisuals.BodyVisible(visual, now), Is.False);
            visual.ResolveTime = now - TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds + .4 + .4);
            mechanics.Update(0);
            Assert.That(BossVigilVisuals.BodyOpacity(visual, now), Is.EqualTo(.5f).Within(.001f));
            Assert.That(entities.GetComponent<BossPresentationComponent>(boss).Hidden, Is.False);
            visual.ResolveTime = now - TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds + .4 + .8);
            Assert.That(BossVigilVisuals.BodyOpacity(visual, now), Is.EqualTo(1).Within(.001));
            FinishVigilRecovery(server, boss);
            Assert.That(entities.GetComponent<TransformComponent>(boss).Coordinates.Position,
                Is.EqualTo(new System.Numerics.Vector2(.5f, .5f)));
            Assert.That(entities.GetComponent<BossPresentationComponent>(boss).Hidden, Is.False);
        });
    }

    [Test]
    public async Task VigilRecoveryAndDamageMarkClockFreezeDuringPause()
    {
        var (server,arena,players,_)=await Setup();
        var boss=arena.Comp.Boss;
        Color pausedAmbient=default;
        double pausedAge=0;
        float pausedEnergy=0;
        await server.WaitAssertion(() =>
        {
            server.System<BossSystem>().StartBossfight(players.ToList(),boss);
            Assert.That(server.System<BossMechanicSystem>().TryStart(boss,new()
                { Telegraph=BossTelegraphPattern.BlackoutNorthWest,CastTime=6,Damage=Hit(1) }),Is.True);
        });
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            server.EntMan.GetComponent<BossMechanicComponent>(boss).Deadline=server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossMechanicSystem>().Update(0);
        });
        await CrystalSeconds(server, .32);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,Is.Zero);
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID,true);
            var visual=server.EntMan.GetComponent<BossVigilComponent>(boss);
            var now=server.ResolveDependency<IGameTiming>().CurTime-server.System<MetaDataSystem>().GetPauseTime(boss);
            pausedAge=(now-visual.ResolveTime).TotalSeconds;
            pausedAmbient=server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor;
            pausedEnergy=server.EntMan.GetComponent<PointLightComponent>(boss).Energy;
        });
        await server.WaitRunTicks(180);
        await server.WaitAssertion(() =>
        {
            var visual=server.EntMan.GetComponent<BossVigilComponent>(boss);
            var now=server.ResolveDependency<IGameTiming>().CurTime-server.System<MetaDataSystem>().GetPauseTime(boss);
            Assert.That((now-visual.ResolveTime).TotalSeconds,Is.EqualTo(pausedAge).Within(.03));
            Assert.That(server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.EqualTo(pausedAmbient));
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,Is.EqualTo(pausedEnergy));
            server.System<SharedMapSystem>().SetPaused(server.EntMan.GetComponent<TransformComponent>(arena.Owner).MapID,false);
        });
        await CrystalSeconds(server, 1.1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossMechanicComponent>(boss),Is.True);
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,Is.InRange(.1f,2.9f));
            Assert.That(server.EntMan.GetComponent<MapLightComponent>(arena.Owner).AmbientLightColor,Is.Not.EqualTo(Color.Black));
        });
        await CrystalSeconds(server, 1.3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<BossVigilComponent>(boss),Is.False);
            Assert.That(server.EntMan.GetComponent<PointLightComponent>(boss).Energy,Is.EqualTo(3));
        });
    }
}
