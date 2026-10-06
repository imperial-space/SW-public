using Content.Shared.Item;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyStationComponent : Component
{
    [DataField] public string ContainerId = "alchemy_items";
    [DataField] public int Capacity = 60;
    [DataField] public ProtoId<ItemSizePrototype> MaxItemSize = "Huge";
    public Container Container = default!;
}
