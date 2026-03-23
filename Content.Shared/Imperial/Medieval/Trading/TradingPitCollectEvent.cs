using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Trading;

[Serializable, NetSerializable]
public sealed partial class TradingPitCollectDoAfterEvent : DoAfterEvent
{
    [DataField("entities", required: true)]
    public IReadOnlyList<NetEntity> Entities = default!;

    private TradingPitCollectDoAfterEvent()
    {
    }

    public TradingPitCollectDoAfterEvent(List<NetEntity> entities)
    {
        Entities = entities;
    }

    public override DoAfterEvent Clone() => this;
}
