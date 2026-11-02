namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

/// <summary>Tracks only currently locked entities; no per-tick target scans.</summary>
[RegisterComponent]
public sealed partial class MeleeLockOnTrackedTargetComponent : Component
{
    public readonly HashSet<EntityUid> Watchers = new();
}
