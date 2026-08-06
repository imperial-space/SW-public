using Content.Server.Imperial.Sponsors;

namespace Content.Server.Imperial.Entry;


public sealed partial class ImperialEntry
{
    public static void Init()
    {
        IoCManager.Resolve<SponsorsManager>().Initialize();
        MedievalInit();
    }

    public static void PostInit()
    {
        MedievalPostInit();
    }

    public static void IoCRegister(IDependencyCollection deps)
    {
        deps.Register<SponsorsManager>();
        MedievalIoCRegister(deps);
    }
}
