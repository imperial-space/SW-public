using Content.Server.Imperial.Medieval.RemoteStore.Events;
using Content.Server.Store.Systems;
using Content.Shared.Store.Components;
using JetBrains.Annotations;

namespace Content.Server.Imperial.Medieval.RemoteStore.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed partial class RemoteStoreSystem : EntitySystem
{

    [Dependency] private readonly StoreSystem _store = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        InitReputation();
    }


    [PublicAPI]
    public void RegisterClient(
        Entity<Components.RemoteStoreServerComponent?> server,
        Entity<Components.RemoteStoreClientComponent?> client,
        EntityUid? requester = null
    )
    {
        if (!Resolve(server, ref server.Comp) || !Resolve(client, ref client.Comp))
            return;

        if (!server.Comp.ConnectedStores.Add(client))
            return;

        client.Comp.ConnectedTo = server;

        SyncStores(server.Owner, client.Owner, requester);
    }

    [PublicAPI]
    public void UnregisterClient(Entity<Components.RemoteStoreClientComponent?> client, EntityUid? requester = null)
    {
        if (!Resolve(client, ref client.Comp))
            return;

        if (client.Comp.ConnectedTo is not { } server)
            return;

        UnregisterClient(server, client, requester);
    }

    [PublicAPI]
    public void UnregisterClient(Entity<Components.RemoteStoreServerComponent?> server,
        Entity<Components.RemoteStoreClientComponent?> client,
        EntityUid? requester = null)
    {
        if (!Resolve(server, ref server.Comp) || !Resolve(client, ref client.Comp))
            return;

        UnregisterClientPrivate(server!, client!, requester);
    }

    private void UnregisterClientPrivate(
        Entity<Components.RemoteStoreServerComponent> server,
        Entity<Components.RemoteStoreClientComponent> client,
        EntityUid? requester = null
    )
    {
        server.Comp.ConnectedStores.Remove(client);
        client.Comp.ConnectedTo = null;

        SyncStores(server.Owner, client.Owner, requester);
    }


    private void SyncStores(Entity<StoreComponent?> primary,
        Entity<StoreComponent?> target,
        EntityUid? requester = null)
    {
        if (!Resolve(primary, ref primary.Comp) || !Resolve(target, ref target.Comp))
            return;

        target.Comp.Name = primary.Comp.Name;
        target.Comp.FullListingsCatalog = primary.Comp.FullListingsCatalog;
        target.Comp.Categories = primary.Comp.Categories;

        var ev = new RemoteStoreSyncedEvent(primary, target, requester);
        RaiseLocalEvent(target, ev, true);

        _store.RefreshAllListings(target.Comp);
    }
}
