using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared.NocturnBitten;

[RegisterComponent]
public sealed partial class NocturnBittenComponent : Component
{
    public float TimeBeforeRemove = 180;
}
