using Robust.Shared.Map;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// Added to a caster for the duration of a cast. While it is present, the caster keeps facing <see cref="Target"/>.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalCastFacingComponent : Component
{
    [ViewVariables]
    public MapCoordinates Target;
}
