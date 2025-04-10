using Robust.Shared.Audio;
using Content.Shared.Storage;
namespace Content.Server.Cargo.Components;


/// <summary>
/// This is used for GPS Tracker Remover, a tool that increases the price of valuables.
/// </summary>
[RegisterComponent]
public sealed partial class GPSTrackerRemoverComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("delay")]
    public float Delay = 10f;

    [DataField("useSound")]
    public SoundSpecifier UseSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/weak_bang.ogg");

    /// <summary>
    /// The entity to spawn when GPS tracker is extracted
    /// </summary>

    [DataField("itemsToSpawn")]
    public List<EntitySpawnEntry> ItemsToSpawn = new();
}
