using Robust.Shared.Audio;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultTeleportComponent : Component
{
    [DataField]
    public bool Enabled = false;
    [DataField]
    public bool Base = true;
    [DataField]
    public int Sector = 0;

}
