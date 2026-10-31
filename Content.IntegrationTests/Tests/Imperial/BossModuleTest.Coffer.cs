using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    [Test]
    public async Task CofferUsesTheAvailableWeapon()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() => server.System<BossSystem>().DamageBoss(arena.Comp.Boss, 100000));
        await server.WaitRunTicks(65);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var coffers = entities.AllEntities<BossEncounterRewardComponent>()
                .Where(coffer => entities.GetComponent<TransformComponent>(coffer).MapUid == arena.Owner).ToArray();
            Assert.That(coffers, Has.Length.EqualTo(1));
            var contents = entities.GetComponent<EntityStorageComponent>(coffers[0]).Contents.ContainedEntities;
            var prototype = server.ResolveDependency<IPrototypeManager>().HasIndex<EntityPrototype>("RevenantBossScythe")
                ? "RevenantBossScythe" : "MedievalScythe";
            Assert.That(contents.Count(item => entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == prototype), Is.EqualTo(1));
        });
    }
}
