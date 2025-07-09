using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.RemoteStore.Components;

/// <summary>
/// I know, that is shit code...
/// </summary>
[RegisterComponent]
public sealed partial class StoreIconComponent : Component
{
    [DataField]
    public SpriteSpecifier.Rsi Icon; // KILL ME
}
