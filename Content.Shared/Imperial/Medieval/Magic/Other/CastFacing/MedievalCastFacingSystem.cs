using Content.Shared.Interaction;

namespace Content.Shared.Imperial.Medieval.Magic;


/// <summary>
/// Keeps the caster turned towards the target point while a spell with <see cref="MedievalCastFacingComponent"/> is being cast.
/// </summary>
public sealed class MedievalCastFacingSystem : EntitySystem
{
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalCastFacingComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            _rotateToFace.TryFaceCoordinates(uid, component.Target.Position);
        }
    }
}
