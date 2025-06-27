using Robust.Shared.GameStates;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent]
public sealed partial class CursedMarkComponent : Component
{
    [DataField]
    public float Delay = 6f;

    public TimeSpan ExplodeTime = TimeSpan.Zero;
}
