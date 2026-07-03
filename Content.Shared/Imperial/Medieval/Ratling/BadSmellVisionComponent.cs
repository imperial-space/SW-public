using Robust.Shared.GameStates;

namespace Content.Shared.Ratling;

[RegisterComponent, NetworkedComponent]
public sealed partial class BadSmellVisionComponent : Component
{
    [DataField]
    public float Radius = 15f;

    [DataField]
    public float SmellThreshold = 60f;
    public HashSet<EntityUid> VisibleTargets = new();
}
