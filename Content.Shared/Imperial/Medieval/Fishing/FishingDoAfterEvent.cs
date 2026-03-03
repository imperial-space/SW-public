using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Fishing;

[Serializable, NetSerializable]
public sealed partial class FishingDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates ClickLocation { get; private set; }

    private FishingDoAfterEvent()
    {
    }

    public FishingDoAfterEvent(NetCoordinates clickLocation)
    {
        ClickLocation = clickLocation;
    }

    public override DoAfterEvent Clone()
    {
        return new FishingDoAfterEvent(ClickLocation);
    }
}
