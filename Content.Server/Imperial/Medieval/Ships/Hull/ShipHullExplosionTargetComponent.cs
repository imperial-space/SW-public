using Robust.Shared.Maths;

namespace Content.Server.Imperial.Medieval.Ships.Hull;

[RegisterComponent]
public sealed partial class ShipHullExplosionTargetComponent : Component
{
    public EntityUid Grid;
    public Vector2i GridIndices;
}
