using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Nocturn.Components;

public sealed partial class AncientNocturneEmergencyTeleportActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class AncientNocturneEmergencyTeleportDoAfterEvent : DoAfterEvent
{
    public readonly NetEntity Action;

    public AncientNocturneEmergencyTeleportDoAfterEvent(NetEntity action)
    {
        Action = action;
    }

    public override DoAfterEvent Clone() => new AncientNocturneEmergencyTeleportDoAfterEvent(Action);
}
