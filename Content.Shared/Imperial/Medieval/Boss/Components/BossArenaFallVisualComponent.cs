using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent]
public sealed partial class BossArenaFallVisualComponent : Component
{
    [DataField] public TimeSpan AnimationTime = TimeSpan.FromSeconds(1.5);
    [DataField] public TimeSpan ReturnDelay = TimeSpan.FromSeconds(1.8);
    public Vector2 OriginalScale = Vector2.Zero;
    public Vector2 AnimationScale = new(0.01f, 0.01f);
}
