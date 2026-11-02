// Imperial medieval start
using Content.Shared.Atmos;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.GameStates;
namespace Content.Shared.Explosion.Components;
/// <summary>Read-only presentation data from the server's native airtight explosion calculation.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(SharedExplosionSystem))]
public sealed partial class ExplosionBlockerComponent : Component
{
    [AutoNetworkedField] public AtmosDirection BlockedDirections;
    // Both sides index explosion prototypes in ordinal ID order. Only the server calculates these values.
    [AutoNetworkedField] public float[] Tolerance = Array.Empty<float>();
}
// Imperial medieval end
