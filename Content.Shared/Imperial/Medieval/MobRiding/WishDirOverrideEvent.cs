using System.Numerics;

namespace Content.Shared.Imperial.Medieval.MobRiding;

[ByRefEvent]
public record struct WishDirOverrideEvent(EntityUid Uid, Vector2 WishDir)
{
    public Vector2 WishDir = WishDir;
}
