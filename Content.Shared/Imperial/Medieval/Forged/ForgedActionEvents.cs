using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Forged;

public sealed partial class ForgedBloodEyesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ForgedBoostActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ForgedSilaActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ForgedRepairActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
