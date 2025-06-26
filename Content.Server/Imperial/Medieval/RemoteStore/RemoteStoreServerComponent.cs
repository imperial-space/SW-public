namespace Content.Server.Imperial.Medieval.RemoteStore;

/// <summary>
/// This is used for
/// </summary>
[RegisterComponent]
public sealed partial class RemoteStoreServerComponent : Component
{
    [DataField]
    public HashSet<EntityUid> ConnectedStores = [];

    [DataField]
    public Dictionary<EntityUid, int> MindsReputation = new();
}
