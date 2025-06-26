namespace Content.Server.Imperial.Medieval.RemoteStore;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class StoreReputationKeepComponent : Component
{
    /// <summary>
    /// Servers, where mind have reputation records
    /// </summary>
    public HashSet<EntityUid> Servers = [];
}
