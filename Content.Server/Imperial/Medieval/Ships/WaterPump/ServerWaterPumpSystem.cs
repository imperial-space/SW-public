using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.WaterPump;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.Ships.WaterPump;

public sealed class ServerWaterPumpSystem : EntitySystem
{
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpComponent, PumpUseEvent>(OnPumpUse);
    }

    private void OnPumpUse(EntityUid uid, WaterPumpComponent component, PumpUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
            return;

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
        _audio.PlayPvs(MedievalShipSounds.PumpUse, uid);
        args.Repeat = true;
        args.Handled = true;
    }
}
