using Content.Shared.DoAfter;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Repairing;

[Serializable, NetSerializable]
public sealed partial class RepairUseEvent : SimpleDoAfterEvent
{
    public Vector2i TileCoordinates;

    public RepairUseEvent(Vector2i tileCoordinates)
    {
        TileCoordinates = tileCoordinates;
    }

    private RepairUseEvent()
    {
    }
}
