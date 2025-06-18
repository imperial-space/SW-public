namespace Content.Server.Imperial.Medieval.RemoteStore.Events;


public sealed class RemoteStoreSyncedEvent(EntityUid Primary, EntityUid Target, EntityUid? Requester) : EntityEventArgs;
