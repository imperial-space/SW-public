using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{
    [TestCase("unattributed")] [TestCase("outside")]
    [TestCase("dead")] [TestCase("other-fight")]
    public async Task CrystalRejectsInvalidDamageSourceWithoutSummoningOrConsumingHealth(string reason)
    {
        var (server, arena, players, gate) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var user = players[0];
            var crystal = arena.Comp.Crystal!.Value;
            if (reason == "outside") server.System<SharedTransformSystem>().SetCoordinates(user,
                entities.GetComponent<TransformComponent>(gate).Coordinates);
            if (reason == "dead") server.System<MobStateSystem>().ChangeMobState(user, MobState.Dead);
            if (reason == "other-fight") entities.EnsureComponent<BossLegacyParticipantComponent>(user);
            server.System<DamageableSystem>().TryChangeDamage(crystal, Hit(1000), origin: reason == "unattributed" ? null : user);
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.False);
            Assert.That(entities.GetComponent<DamageableComponent>(crystal).TotalDamage.Float(), Is.Zero);
            Assert.That(arena.Comp.Crystal, Is.EqualTo(crystal));
        });
    }

    [Test]
    public async Task CrystalUsesSpellOwnerForFinalHitAndAllowsRangedDamage()
    {
        var (server, arena, players, _) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var caster = players[1];
            server.System<SharedTransformSystem>().SetCoordinates(caster, new EntityCoordinates(arena.Comp.CombatGrid, 4.5f, 4.5f));
            var spell = entities.SpawnEntity(null, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
            server.System<SharedBossArenaRulesSystem>().TagSpell(spell, caster);
            server.System<DamageableSystem>().TryChangeDamage(arena.Comp.Crystal!.Value, Hit(100), origin: spell);
            Assert.That(entities.EntityExists(arena.Comp.Boss), Is.True);
            Assert.That(entities.GetComponent<BossFocusMarkerComponent>(arena.Comp.Boss).Tank, Is.EqualTo(caster));
        });
    }

    [Test]
    public async Task CrystalAcceptsNativePreciseAndWideMelee()
    {
        var (server, arena, players, _) = await Setup(crystalOnly: true);
        await server.WaitAssertion(() =>
        {
            server.System<SharedTransformSystem>().SetCoordinates(players[0], new EntityCoordinates(arena.Comp.CombatGrid, 1.5f, 0.5f));
            server.System<SharedCombatModeSystem>().SetInCombatMode(players[0], true);
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var crystal = arena.Comp.Crystal!.Value;
            var health = entities.GetComponent<DamageableComponent>(crystal);
            var weapon = entities.GetComponent<MeleeWeaponComponent>(players[0]);
            Assert.That(server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(players[0], players[0], weapon, crystal), Is.True);
            Assert.That(health.TotalDamage.Float(), Is.GreaterThan(0));
            var before = health.TotalDamage;
            weapon.NextAttack = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(server.System<SharedMeleeWeaponSystem>().AttemptHeavyAttack(players[0], players[0], weapon,
                entities.GetComponent<TransformComponent>(crystal).Coordinates), Is.True);
            Assert.That(health.TotalDamage, Is.GreaterThan(before));
        });
    }
}
