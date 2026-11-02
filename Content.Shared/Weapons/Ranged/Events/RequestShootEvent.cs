using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing; // Imperial medieval edit

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    public NetEntity Gun;
    public GameTick ProjectileInputTick; // Imperial medieval edit - firing identity; never a client hit claim.
    public NetCoordinates Coordinates;
    public NetEntity? Target;
}
