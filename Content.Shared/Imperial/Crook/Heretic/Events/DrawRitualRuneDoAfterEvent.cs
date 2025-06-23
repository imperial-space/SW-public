using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.Heretic.Events;

[Serializable, NetSerializable]
public sealed partial class DrawRitualRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity AnimationEntity;

    [DataField]
    public NetCoordinates Coordinates;

    public DrawRitualRuneDoAfterEvent()
    {
    }

    public DrawRitualRuneDoAfterEvent(NetEntity animationEntity, NetCoordinates coordinates)
    {
        AnimationEntity = animationEntity;
        Coordinates = coordinates;
    }
}
