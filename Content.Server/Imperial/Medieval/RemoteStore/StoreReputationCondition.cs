using Content.Server.Imperial.Medieval.RemoteStore.Components;
using Content.Server.Imperial.Medieval.RemoteStore.Systems;
using Content.Shared.Store;

namespace Content.Server.Imperial.Medieval.RemoteStore;

public sealed partial class StoreReputationCondition : ListingCondition
{
    [DataField(required: true)]
    public int RequiredReputation;

    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;
        if (!ent.TryGetComponent<RemoteStoreClientComponent>(args.StoreEntity, out var clientComp))
            return false;
        if (!clientComp.IsConnected)
            return false;
        var remoteStore = ent.System<RemoteStoreSystem>();
        if (!remoteStore.TryGetReputation(clientComp.ConnectedTo!.Value, args.Buyer, out var reputation))
            return false;
        if (reputation >= RequiredReputation)
            return true;
        return false;
    }
}
