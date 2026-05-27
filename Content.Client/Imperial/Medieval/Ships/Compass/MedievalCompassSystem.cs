using System.Numerics;
using Content.Shared.Imperial.Medieval.Ships.Compass;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Ships.Compass;

public sealed class MedievalCompassSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalCompassComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var rotation = Angle.FromWorldVec(Vector2.UnitY) - sprite.Rotation;

            // Normal sprites receive eye rotation while rendering; no-rotation sprites do not.
            rotation += sprite.NoRotation
                ? _eyeManager.CurrentEye.Rotation
                : -_transform.GetWorldRotation(xform);

            _sprite.LayerSetRotation((uid, sprite), MedievalCompassLayers.Arrow, rotation);
        }
    }
}
