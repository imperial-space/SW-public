using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.Ships.Repairing;

public sealed class ServerShipRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RepairMaterialComponent, RepairUseEvent>(OnRepairUse);
    }

    private void OnRepairUse(EntityUid uid, RepairMaterialComponent component, RepairUseEvent args)
    {
        if (args.Cancelled || !args.Handled)
            return;

        _audio.PlayPvs(MedievalShipSounds.HammerUse, uid);
    }
}
