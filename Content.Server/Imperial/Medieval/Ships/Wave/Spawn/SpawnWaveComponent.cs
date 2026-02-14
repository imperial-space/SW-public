namespace Content.Server.Imperial.Medieval.Ships.Wave.Spawn;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SpawnWaveComponent : Component
{
    [DataField]
    public bool DeleteOnCollide = true;
}
