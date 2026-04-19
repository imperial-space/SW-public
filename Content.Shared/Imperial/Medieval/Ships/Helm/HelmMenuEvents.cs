using System;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Helm;

[Serializable, NetSerializable]
public enum HelmMenuAction : byte
{
    RotateLeft,
    Center,
    RotateRight
}

[Serializable, NetSerializable]
public sealed class OpenHelmMenuEvent : EntityEventArgs
{
    public int Target;

    public OpenHelmMenuEvent(int target)
    {
        Target = target;
    }

    public OpenHelmMenuEvent()
    {
    }
}

[Serializable, NetSerializable]
public sealed class HelmMenuActionEvent : EntityEventArgs
{
    public HelmMenuAction Action;
    public int Target;

    public HelmMenuActionEvent(HelmMenuAction action, int target)
    {
        Action = action;
        Target = target;
    }

    public HelmMenuActionEvent()
    {
    }
}

[Serializable, NetSerializable]
public sealed partial class HelmActionDoAfterEvent : DoAfterEvent
{
    public HelmMenuAction Action;

    public HelmActionDoAfterEvent(HelmMenuAction action)
    {
        Action = action;
    }

    public override DoAfterEvent Clone() => new HelmActionDoAfterEvent(Action);
}
