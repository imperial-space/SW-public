using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossHitFeedbackSystem : EntitySystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _camera = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public void OnHit(EntityUid boss, EntityUid target, DamageSpecifier? damage, bool tankBuster)
    {
        if (damage == null || !damage.AnyPositive() || !Exists(target) || Terminating(target)) return;
        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
        var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(boss);
        if (direction.LengthSquared() < 0.000001f) direction = new Vector2(1, -1);
        direction = Vector2.Normalize(direction);

        _camera.KickCamera(target, direction * (tankBuster ? 0.9f : 0.22f));

        if (!tankBuster) _guns.PlayImpactSound(target, damage, null, false);
    }
}
