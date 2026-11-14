using Robust.Shared.Map;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// A projectile with this component triggers when it reaches the point it was aimed at.
/// The point is set by the server when the spell is cast.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalDetonateAtTargetComponent : Component
{
    [ViewVariables]
    public MapCoordinates? Target;
}
