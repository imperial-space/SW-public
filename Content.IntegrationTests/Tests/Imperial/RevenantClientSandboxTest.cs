using Robust.Server;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantClientSandboxTest
{
    [Test]
    public async Task ClientAndSharedAssembliesPassRuntimeSandbox()
    {

        using var engine = new RobustIntegrationTest.ServerIntegrationInstance(new()
        {
            ContentStart = false,
            LoadTestAssembly = false,
            Options = new ServerOptions
            {
                LoadContentResources = false,
                LoadConfigAndUserData = false,
                PrototypeDirectory = new("/ClientSandboxNoWorldAssets/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {

            var loaderType = typeof(IModLoader).Assembly.GetType("Robust.Shared.ContentPack.ModLoader", throwOnError: true)!;
            using var loader = (IDisposable)Activator.CreateInstance(loaderType)!;
            IoCManager.InjectDependencies(loader);
            ((IPostInjectInit)loader).PostInject();
            loaderType.GetMethod("SetUseLoadContext")!.Invoke(loader, [false]);
            loaderType.GetMethod("SetEnableSandboxing")!.Invoke(loader, [true]);
            var check = loaderType.GetMethod("LoadGameAssembly", [typeof(string), typeof(bool)])!;
            check.Invoke(loader, [typeof(Client.Entry.EntryPoint).Assembly.Location, false]);
            check.Invoke(loader, [typeof(Shared.Entry.EntryPoint).Assembly.Location, false]);
        });
    }
}
