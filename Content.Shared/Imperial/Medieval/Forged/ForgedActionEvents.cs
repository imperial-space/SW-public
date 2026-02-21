using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed partial class ThermalEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
