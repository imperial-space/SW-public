using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankAmmoComponent : Component
{

}

[NetSerializable, Serializable]
public sealed partial class ShiftTankLoadDoAfter : SimpleDoAfterEvent
{
    public EntProtoId AmmoType;
}
