using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class TradingPitCollectComponent : Component
{
    [DataField("radius")]
    public float Radius = 1.5f;

    [DataField("delayPerWeight")]
    public float DelayPerWeight = SharedStorageSystem.AreaInsertDelayPerItem;

    [DataField("pickupLimit")]
    public int PickupLimit = StorageComponent.AreaPickupLimit;
}
