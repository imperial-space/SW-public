using System.Linq;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Exceptions;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial.Medieval;

/// <summary>
/// A scoped, real content server for book tests that assert server-side behavior only.
/// It loads no client and creates only the test's one-tile grid.
/// Every scope owns a fresh server; no pending ability work survives into another test.
/// </summary>
internal sealed class BookTestServer : RobustIntegrationTest, IAsyncDisposable
{
    public ServerIntegrationInstance Server { get; private set; } = default!;
    private bool _disposed;

    private BookTestServer()
    {
    }

    public static async Task<BookTestServer> Create()
    {
        var test = new BookTestServer();
        var options = new ServerIntegrationOptions
        {
            Pool = false,
            ContentStart = true,
            LoadTestAssembly = false,
            FailureLogLevel = LogLevel.Error,
            ContentAssemblies =
            [
                typeof(Content.Shared.Entry.EntryPoint).Assembly,
                typeof(Content.Server.Entry.EntryPoint).Assembly,
            ],
            Options = new()
            {
                LoadConfigAndUserData = false,
                LoadContentResources = true,
            },
        };
        foreach (var (cvar, value) in PoolManager.TestCvars)
            options.CVarOverrides[cvar] = value;
        options.CVarOverrides[CVars.NetPVS.Name] = "false";
        options.CVarOverrides[CVars.ThreadParallelCount.Name] = "1";
        options.CVarOverrides[CVars.ReplayServerRecordingEnabled.Name] = "false";
        options.CVarOverrides[CCVars.GameDummyTicker.Name] = "true";
        options.CVarOverrides[CCVars.GameMap.Name] = "Empty";

        test.Server = test.StartServer(options);
        try
        {
            await test.Server.WaitIdleAsync();
            return test;
        }
        catch
        {
            await test.DisposeAsync();
            throw;
        }
    }

    /// <summary>Matches the real one-tile map created by TestPair.CreateTestMap.</summary>
    public async Task<TestMapData> CreateTestMap(bool initialized = true)
    {
        var map = new TestMapData();
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var tileId = Server.Resolve<ITileDefinitionManager>()["Plating"].TileId;
            map.MapUid = maps.CreateMap(out map.MapId, runMapInit: initialized);
            map.Grid = Server.MapMan.CreateGridEntity(map.MapId);
            map.GridCoords = new EntityCoordinates(map.Grid, 0, 0);
            map.MapCoords = new MapCoordinates(0, 0, map.MapId);
            maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords, new Tile(tileId));
            map.Tile = maps.GetAllTiles(map.Grid.Owner, map.Grid.Comp).First();
        });
        return map;
    }

    /// <summary>Compatibility with server-only callers of TestPair.RunTicksSync.</summary>
    public Task RunTicksSync(int ticks) => Server.WaitRunTicks(ticks);

    public async Task CleanReturnAsync()
    {
        if (_disposed) return;
        await Server.WaitIdleAsync();
        await Server.Cleanup();
        await Server.WaitPost(() => Server.EntMan.FlushEntities());
        Assert.That(Server.IsAlive, Is.True, "The book test unexpectedly stopped its server.");
        Assert.That(Server.Resolve<IRuntimeLog>().ExceptionCount, Is.Zero,
            "The book test must not leave server runtime exceptions.");
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Server.Stop();
        // Preserve the original test/startup exception if this is a failing scope.
        await Server.WaitIdleAsync(throwOnUnhandled: false);
    }
}
