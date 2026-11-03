using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.RPPanel;

[Serializable, NetSerializable]
public enum RPPanelAction : byte
{
    Look,
    Wave,
    Hug,
    Shake
}

[Serializable, NetSerializable]
public sealed class RPPanelInteractionEvent(NetEntity target, RPPanelAction action) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly RPPanelAction Action = action;
}

[ByRefEvent]
public readonly record struct RPPanelActionEvent(EntityUid User, RPPanelAction Action);
