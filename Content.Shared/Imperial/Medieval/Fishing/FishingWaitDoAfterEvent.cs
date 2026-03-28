using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Fishing;

[Serializable, NetSerializable]
public sealed partial class FishingWaitDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity? Bobber { get; private set; }

    [DataField]
    public int CurrentChance { get; set; } = 1;

    private FishingWaitDoAfterEvent()
    {
    }

    public FishingWaitDoAfterEvent(NetEntity? bobber, int currentChance = 1)
    {
        Bobber = bobber;
        CurrentChance = currentChance;
    }

    public override DoAfterEvent Clone()
    {
        return new FishingWaitDoAfterEvent(Bobber, CurrentChance);
    }
}
