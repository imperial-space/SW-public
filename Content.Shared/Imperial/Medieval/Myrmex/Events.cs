using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared.Imperial.Medieval.Myrmex;

[Serializable, NetSerializable]
public sealed partial class StewFeedDoAfterEvent : DoAfterEvent
{
    public StewFeedDoAfterEvent() {}
    public override DoAfterEvent Clone() => this;
}

[Serializable]
public sealed partial class LarvaFeedEvent : EntityEventArgs
{
    public EntityPrototype Eaten;

    public LarvaFeedEvent(EntityPrototype eaten)
    {
        Eaten = eaten;
    }
}

public sealed partial class ActionMyrmexQueenLayEggEvent : InstantActionEvent;
