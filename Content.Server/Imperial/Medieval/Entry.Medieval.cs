using Content.Server.Imperial.Medieval.Achievements.Jobs;
using Content.Server.Imperial.Medieval.Afk;
using Content.Server.Imperial.Medieval.Flavors;
using Content.Server.Imperial.Medieval.JoinQueue;
using Content.Server.Imperial.PVS;
using Content.Shared.Imperial.Medieval.Flavors;

namespace Content.Server.Imperial.Entry;

/// <summary>

/// </summary>
public sealed partial class ImperialEntry
{
    private static void MedievalInit()
    {
        IoCManager.Resolve<ServerFlavorManager>().Init();
        IoCManager.Resolve<JoinQueueManager>().Initialize();
        IoCManager.Resolve<AlwaysPvsSystem>().Initialize();
        IoCManager.Resolve<JobAchievementManager>().Initialize();
    }

    private static void MedievalPostInit()
    {
    }

    private static void MedievalIoCRegister(IDependencyCollection deps)
    {
        deps.Register<JoinQueueManager>();
        deps.Register<AlwaysPvsSystem>();
        deps.Register<IMedievalAfkManager, MedievalAfkManager>();
        deps.Register<ServerFlavorManager>();
        deps.Register<SharedFlavorManager, ServerFlavorManager>();
        deps.Register<JobAchievementManager>();
    }
}
