using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossArenaRulesModuleTest : RevenantIntegrationTestBase
{
    [Test]
    public async Task DamageRulesRemainIndependentAndKeepHealing()
    {
        var options = new ServerIntegrationOptions
        {
            Pool = false,
            ContentStart = true,
            LoadTestAssembly = false,
            ContentAssemblies = [typeof(Content.Server.Entry.EntryPoint).Assembly, typeof(Content.Shared.Entry.EntryPoint).Assembly],
            Options = new() { LoadConfigAndUserData = false },
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;
        var server = StartServer(options);
        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var map = maps.CreateMap(out _, runMapInit: true);
            var settings = entities.AddComponent<BossArenaRulesComponent>(map);
            var attacker = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var ally = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.One));
            entities.AddComponent<BossArenaPartyMemberComponent>(attacker);
            entities.AddComponent<BossArenaPartyMemberComponent>(ally);
            var rules = server.System<SharedBossArenaRulesSystem>();
            var features = server.System<BossFeatureSystem>();
            var kinds = new[] { BossArenaDamageKind.Direct, BossArenaDamageKind.Spells, BossArenaDamageKind.Melee,
                BossArenaDamageKind.Projectiles, BossArenaDamageKind.Explosions,
                BossArenaDamageKind.Contact, BossArenaDamageKind.Fire };
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(10);
            damage.DamageDict["Heat"] = FixedPoint2.New(-2);
            foreach (var allowed in kinds)
            {
                settings.AllowedFriendlyDamage = allowed;
                foreach (var kind in kinds)
                {
                    var installed = features.Contains("arena-friendly-fire-" + kind.ToString().ToLowerInvariant());
                    var denied = installed && kind != allowed;
                    using var source = rules.WithSource(attacker, kind);
                    var result = rules.FilterDamage(ally, damage, attacker);
                    Assert.That(result.DamageDict.GetValueOrDefault("Blunt"),
                        Is.EqualTo(denied ? FixedPoint2.Zero : FixedPoint2.New(10)), $"{kind}, allowed {allowed}");
                    Assert.That(result.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(-2)));
                }
            }
            entities.AddComponent<BossArenaProtectedComponent>(ally);
            Assert.That(rules.BlocksControl(ally, attacker), Is.EqualTo(features.Contains("arena-control-denial")));
            settings.AllowControl = true;
            Assert.That(rules.BlocksControl(ally, attacker), Is.False);
            Assert.That(rules.CanSpawn("WallSolid", new EntityCoordinates(map, Vector2.Zero)),
                Is.EqualTo(!features.Contains("arena-building-denial")));
            settings.AllowBuilding = true;
            Assert.That(rules.CanSpawn("WallSolid", new EntityCoordinates(map, Vector2.Zero)), Is.True);
            Assert.That(rules.CanUseEntityStorage(ally, attacker), Is.EqualTo(!features.Contains("arena-equipment-denial")));
            settings.AllowEquipment = true;
            Assert.That(rules.CanUseEntityStorage(ally, attacker), Is.True);
            entities.AddComponent<BossArenaAttackSourceComponent>(attacker);
            Assert.That(rules.PreservesArmor(ally, attacker), Is.EqualTo(features.Contains("arena-armor-protection")));
            settings.AllowArmorWear = true;
            Assert.That(rules.PreservesArmor(ally, attacker), Is.False);
            entities.AddComponent<BossActorComponent>(attacker);
            settings.AllowedFriendlyDamage = BossArenaDamageKind.Direct;
            using (rules.WithSource(attacker, BossArenaDamageKind.Explosions))
                Assert.That(rules.FilterDamage(ally, damage, attacker).DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(10)));
        });
    }
}
