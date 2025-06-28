using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.RemoteStore.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class RandomStoreComponent : Component
{
    [DataField]
    public ProtoId<RandomStorePresetPrototype> StorePreset;
}
