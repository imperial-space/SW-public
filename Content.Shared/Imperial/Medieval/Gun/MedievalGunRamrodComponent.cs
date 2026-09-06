using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Gun;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalGunRamrodComponent : Component
{
    [DataField]
    public SoundSpecifier? ActionSound;
}
