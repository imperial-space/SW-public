using Content.Client.Imperial.ShockWave;
using Content.Client.Imperial.Sponsors;
using Content.Client.Imperial.Medieval.ShipDrowning;
using Content.Client.Imperial.Medieval.Ships.Wind;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Entry;


public sealed partial class ImperialEntry
{
    public static void Init()
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        prototypeManager.RegisterIgnore("stationGoal");
        prototypeManager.RegisterIgnore("ertCall");
    }

    public static void PostInit()
    {
        var overlayManager = IoCManager.Resolve<IOverlayManager>();

        overlayManager.AddOverlay(new ShockWaveDistortionOverlay());
        overlayManager.AddOverlay(new SeaSwellOverlay());
        overlayManager.AddOverlay(new SeaShipFloodOverlay());
        overlayManager.AddOverlay(new SeaShipRippleOverlay());
        overlayManager.AddOverlay(new SeaWindOverlay());

        IoCManager.Resolve<SponsorsManager>().Initialize();
    }

    public static void IoCRegister()
    {
        //IoCManager.Register<SponsorsManager>(); //Imperial sponsors
    }
}
