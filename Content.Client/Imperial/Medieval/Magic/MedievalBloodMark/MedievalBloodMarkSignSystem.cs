using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic.MedievalBloodMark;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Magic.MedievalBloodMark;


public sealed class MedievalBloodMarkSignSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<MedievalBloodMarkSignComponent, SpriteComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var sign, out var sprite, out var xform))
        {
            if (!TryComp<SpriteComponent>(xform.ParentUid, out var targetSprite))
                continue;

            var offset = _sprite.GetLocalBounds((xform.ParentUid, targetSprite)).Top + sign.Margin;

            if (sign.AppliedOffset is { } applied && MathF.Abs(applied - offset) < 0.01f)
                continue;

            sign.AppliedOffset = offset;
            _sprite.SetOffset((uid, sprite), new Vector2(0, offset));
        }
    }
}
