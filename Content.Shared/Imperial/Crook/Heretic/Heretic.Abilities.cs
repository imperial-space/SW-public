using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.DoAfter;

namespace Content.Shared.Heretic;

[Serializable, NetSerializable]
public sealed partial class DrawRitualRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("animationEntity")]
    public NetEntity AnimationEntity { get; private set; }

    [DataField("coordinates")]
    public NetCoordinates Coordinates { get; private set; }

    public DrawRitualRuneDoAfterEvent(NetEntity animationEntity, NetCoordinates coordinates)
    {
        AnimationEntity = animationEntity;
        Coordinates = coordinates;
    }
}
