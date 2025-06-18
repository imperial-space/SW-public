namespace Content.Server.Imperial.Medieval.RemoteStore;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class RemoteStoreClientComponent : Component
{
    [ViewVariables]
    public bool IsConnected => ConnectedTo is not null;

    [DataField]
    public EntityUid? ConnectedTo;
}
