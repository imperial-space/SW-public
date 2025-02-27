using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared.NocturnBitten;

[RegisterComponent]
public sealed partial class ZveresScreamComponent : Component
{
    [DataField]
    public float TimeBeforeRemove = 0f;
}
