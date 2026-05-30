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

            rotation += sprite.NoRotation
                ? _eyeManager.CurrentEye.Rotation
                : -_transform.GetWorldRotation(xform);

            var snappedIndex = (int) Math.Round(rotation.Reduced().Theta / (Math.PI / 4));
            snappedIndex = ((snappedIndex % 8) + 8) % 8;

            _sprite.LayerSetState((uid, sprite), MedievalCompassLayers.Base, MedievalCompassComponent.DirectionStates[snappedIndex]);
        }
    }
}
