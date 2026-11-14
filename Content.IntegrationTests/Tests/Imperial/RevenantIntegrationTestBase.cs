using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[Parallelizable(ParallelScope.Self)]
public abstract class RevenantIntegrationTestBase : RobustIntegrationTest
{
    [TearDown]
    public Task ReleaseTestServers() => OneTimeTearDown();
}
