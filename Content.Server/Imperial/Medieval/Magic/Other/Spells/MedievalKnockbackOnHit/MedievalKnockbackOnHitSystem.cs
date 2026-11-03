using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic.MedievalKnockbackOnHit;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.TDMNaming.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;

namespace Content.Server.Imperial.Medieval.Magic.MedievalKnockbackOnHit;


public sealed class MedievalKnockbackOnHitSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalKnockbackOnHitComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<MedievalKnockbackOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_mobState.IsAlive(args.Target)) return;
        if (HasComp<MedievalUnthrowableComponent>(args.Target)) return;

        var direction = TryComp<PhysicsComponent>(ent, out var body) && body.LinearVelocity.LengthSquared() > 0.01f
            ? body.LinearVelocity
            : _transform.GetWorldPosition(args.Target) - _transform.GetWorldPosition(ent);

        if (direction.LengthSquared() < 0.0001f) return;

        _throwing.TryThrow(
            args.Target,
            Vector2.Normalize(direction) * ent.Comp.Distance,
            ent.Comp.Speed,
            args.Shooter,
            pushbackRatio: 0f,
            recoil: false,
            animated: false,
            playSound: false,
            doSpin: false);
    }
}
