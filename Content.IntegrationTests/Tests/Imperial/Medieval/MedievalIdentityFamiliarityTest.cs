using Content.Shared.Examine;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Medieval.IdentityManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Imperial.Medieval;

[TestFixture]
public sealed class MedievalIdentityFamiliarityTest
{
    [Test]
    public async Task FamiliarityHintRequiresKnowledgeAndConcealment()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        EntityUid target = default;
        EntityUid familiarViewer = default;
        EntityUid stranger = default;

        await server.WaitPost(() =>
        {
            EntityUid SpawnIdentityHolder()
            {
                var holder = entities.CreateEntityUninitialized(null, map.GridCoords);
                entities.AddComponent<IdentityRequiresKnowledgeComponent>(holder);
                entities.InitializeAndStartEntity(holder, map.MapId);
                return holder;
            }

            target = SpawnIdentityHolder();
            familiarViewer = SpawnIdentityHolder();
            stranger = SpawnIdentityHolder();

            var targetIdentity = entities.GetComponent<IdentityRequiresKnowledgeComponent>(target);
            var viewerIdentity = entities.GetComponent<IdentityRequiresKnowledgeComponent>(familiarViewer);
            viewerIdentity.KnownIds.Add(targetIdentity.Identifier);
            entities.Dirty(familiarViewer, viewerIdentity);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetExamineText(entities, target, familiarViewer), Does.Not.Contain("They seem familiar."));
            Assert.That(GetExamineText(entities, target, stranger), Does.Not.Contain("They seem familiar."));
        });

        await server.WaitPost(() => entities.EnsureComponent<IdentityBlockerComponent>(target));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetExamineText(entities, target, familiarViewer), Does.Contain("They seem familiar."));
            Assert.That(GetExamineText(entities, target, familiarViewer, false), Does.Not.Contain("They seem familiar."));
            Assert.That(GetExamineText(entities, target, stranger), Does.Not.Contain("They seem familiar."));
            Assert.That(GetExamineText(entities, target, target), Does.Not.Contain("They seem familiar."));
        });

        await pair.CleanReturnAsync();
    }

    private static string GetExamineText(
        IEntityManager entities,
        EntityUid target,
        EntityUid examiner,
        bool isInDetailsRange = true)
    {
        var message = new FormattedMessage();
        var examined = new ExaminedEvent(message, target, examiner, isInDetailsRange, false);
        entities.EventBus.RaiseLocalEvent(target, examined);
        return examined.GetTotalMessage().ToMarkup();
    }
}
