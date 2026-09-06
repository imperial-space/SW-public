using Robust.Shared.Audio;

namespace Content.Server.Imperial.ShockWave;


/// <summary>
/// Applies stamina damage to entities hit by a shockwave.
/// </summary>
[RegisterComponent]
public sealed partial class ShockWaveStaminaDamageComponent : Component
{
    [DataField]
    public float Stamina;

    [DataField]
    public SoundSpecifier? Sound;
}
