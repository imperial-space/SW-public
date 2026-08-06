using Content.Client.Imperial.Medieval.CharacterBlock;
using Content.Client.Imperial.Medieval.Flavors;
using Content.Client.Imperial.Medieval.JoinQueue;
using Content.Shared.Imperial.Medieval.Flavors;

namespace Content.Client.Imperial.Entry;

public sealed partial class ImperialEntry
{
    private static void MedievalInit()
    {
        IoCManager.Resolve<JoinQueueManager>().Initialize();
    }

    private static void MedievalPostInit()
    {
        IoCManager.Resolve<CharacterBlockManager>().Initialize();
        IoCManager.Resolve<ClientFlavorManager>().Initialize();
    }

    private static void MedievalShutdown()
    {
        IoCManager.Resolve<ClientFlavorManager>().Shutdown();
    }

    private static void MedievalIoCRegister(IDependencyCollection collection)
    {
        collection.Register<JoinQueueManager>();
        collection.Register<CharacterBlockManager>();
        collection.Register<ClientFlavorManager>();
        collection.Register<SharedFlavorManager, ClientFlavorManager>();
    }
}
