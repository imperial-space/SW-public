using System.Numerics;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ArenaBossHitsPreserveSharpnessWithoutChangingOrdinaryHitsOrMisses(bool wide)
    {
        var (server, arena, players, gate) = await Setup();
        await server.WaitAssertion(() =>
        {
            var boss = arena.Comp.Boss;
            var weapon = server.EntMan.SpawnEntity("MedievalShortSword", server.EntMan.GetComponent<TransformComponent>(players[0]).Coordinates);
            var sharpness = server.EntMan.GetComponent<MedievalMeleeResourceComponent>(weapon);
            sharpness.Resource = 90;
            sharpness.ResourceWaste = 1;
            Vector2? direction = wide ? Vector2.UnitX : null;
            server.EntMan.EventBus.RaiseLocalEvent(weapon, new MeleeHitEvent([boss], players[0], weapon, Hit(1), direction));
            Assert.That(sharpness.Resource, Is.EqualTo(90), "Hitting the arena's boss must not blunt the weapon.");
            server.EntMan.EventBus.RaiseLocalEvent(weapon, new MeleeHitEvent([players[1]], players[0], weapon, Hit(1), direction));
            Assert.That(sharpness.Resource, Is.EqualTo(89), "An ordinary target retains native weapon wear even on the arena map.");
            server.System<BossArenaProtectionSystem>().MoveBoss(boss, server.EntMan.GetComponent<TransformComponent>(gate).Coordinates);
            server.EntMan.EventBus.RaiseLocalEvent(weapon, new MeleeHitEvent([boss], players[0], weapon, Hit(1), direction));
            Assert.That(sharpness.Resource, Is.EqualTo(88), "A boss spawned outside its assigned arena receives no wear exemption.");
            server.EntMan.EventBus.RaiseLocalEvent(weapon, new MeleeHitEvent([], players[0], weapon, Hit(1), direction));
            Assert.That(sharpness.Resource, Is.EqualTo(87), "The native miss behavior stays unchanged.");
        });
    }
}
