// Imperial medieval start
using System.Numerics;
using Content.Shared.Throwing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    [Dependency] private readonly IGameTiming _predictionTiming = default!;

    /// <summary>Register a gun or spell projectile with the same presentation identity.</summary>
    public bool MarkPrediction(EntityUid uid, EntityUid shooter, ProjectilePredictionKey key, EntityUid? source = null)
    {
        if (key.InputTick == GameTick.Zero || !TryComp<ProjectileComponent>(uid, out var projectile) ||
            !projectile.Predict || projectile.PenetrationThreshold != 0)
            return false;
        var marker = new ProjectilePredictionComponent
        {
            Shooter = shooter, Weapon = key.Weapon, InputTick = key.InputTick,
            LaunchTick = _predictionTiming.CurTick, Index = key.Index, Source = source,
        };
        AddComp(uid, marker, true);
        Dirty(uid, marker);
        _physics.UpdateIsPredicted(uid);
        return true;
    }

    /// <summary>The same fixture gate as authoritative projectile collision.</summary>
    public static bool IsProjectileImpact(ProjectileComponent projectile, in StartCollideEvent contact)
    {
        return contact.OurFixtureId == ProjectileFixture && contact.OtherFixture.Hard &&
               !projectile.ProjectileSpent && (!projectile.OnlyCollideWhenShot || projectile.Weapon != null);
    }

    /// <summary>Only the original pose operations. Does not raise gameplay events or register an embedded object.</summary>
    public void ApplyEmbeddingPose(EntityUid uid, EntityUid target, EmbeddableProjectileComponent component)
    {
        TryComp<PhysicsComponent>(uid, out var physics);
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetBodyType(uid, BodyType.Static, body: physics);
        var xform = Transform(uid);
        _transform.SetParent(uid, xform, target);
        if (component.Offset == Vector2.Zero)
            return;
        var rotation = xform.LocalRotation;
        if (TryComp<ThrowingAngleComponent>(uid, out var throwing))
            rotation += throwing.Angle;
        _transform.SetLocalPosition(uid, xform.LocalPosition + rotation.RotateVec(component.Offset), xform);
    }

    public void CompletePrediction(NetEntity weapon, GameTick tick, EntityUid shooter, bool launched)
    {
        if (_net.IsServer && tick != GameTick.Zero)
            RaiseNetworkEvent(new ProjectileShotCompletedEvent(weapon, tick, launched), shooter);
    }

    public void SendImpactPresentation(EntityUid uid, ProjectileImpactPresentationEvent message)
    {
        if (_net.IsServer && TryComp<ProjectilePredictionComponent>(uid, out var prediction))
            RaiseNetworkEvent(message, prediction.Shooter);
    }
}
// Imperial medieval end
