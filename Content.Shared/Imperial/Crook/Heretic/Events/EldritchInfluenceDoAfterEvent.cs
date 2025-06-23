using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

[Serializable, NetSerializable]
public sealed partial class EldritchInfluenceDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
