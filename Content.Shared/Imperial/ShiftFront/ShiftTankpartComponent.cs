using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankpartComponent : Component
{
    [DataField]
    public string Part = "";
    [DataField]
    public EntProtoId Ammo;

    [DataField]
    public SoundSpecifier SoundAmmoLoad = new SoundPathSpecifier("/Audio/Imperial/ShiftFront/reload_tank.ogg");
    [DataField]
    public EntityUid? Tank;

}
