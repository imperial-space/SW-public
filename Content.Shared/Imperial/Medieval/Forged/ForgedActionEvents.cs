using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed partial class BloodEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
public sealed partial class MedicalEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
public sealed partial class InvisibleVisionEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
public sealed partial class NightVisionEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
