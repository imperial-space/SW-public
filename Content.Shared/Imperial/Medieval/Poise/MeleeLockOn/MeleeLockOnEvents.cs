using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

public sealed partial class MeleeLockOnToggleEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class MeleeLockOnTargetEvent(NetEntity? target, bool exitTargeting = false) : EntityEventArgs
{
    public readonly NetEntity? Target = target;
    public readonly bool ExitTargeting = exitTargeting;
}
