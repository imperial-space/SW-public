using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Input;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Siege.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Siege.Events;
using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Physics.Components; // Нужно для управления физикой
using Robust.Shared.Physics.Systems;   // Нужно для управления физикой
using Content.Shared.ShiftFront.Components; // Остальные зависимости оставлены как были
using Content.Shared.Actions;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Shared.XCOM;

public sealed partial class ShiftFrontSharedSystem : EntitySystem
{

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] internal readonly IEntityManager _entityManager = default!;
    [Dependency] internal readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShiftTankBulletComponent, ProjectileBeforeHitEvent>(OnBeforeProjectileHit);
        SubscribeLocalEvent<ShiftTankBulletComponent, PreventCollideEvent>(OnBeforeProjectileCollide);

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.MoveUp, new PointerInputCmdHandler(MovementUp))
            .Bind(EngineKeyFunctions.MoveRight, new PointerInputCmdHandler(MovementRight))
            .Bind(EngineKeyFunctions.MoveDown, new PointerInputCmdHandler(MovementDown))
            .Bind(EngineKeyFunctions.MoveLeft, new PointerInputCmdHandler(MovementLeft))
            .Register<ShiftFrontSharedSystem>();
    }
    private void OnBeforeProjectileCollide(EntityUid uid, ShiftTankBulletComponent component, ref PreventCollideEvent args)
    {
        if (!TryComp<ShiftTankHullComponent>(args.OtherEntity, out var hull)) return;
        if (!hull.LinkedTurret.HasValue) return;
        if (!TryComp<ProjectileComponent>(args.OurEntity, out var proj)) return;
        if (!proj.Shooter.HasValue) return;

        if ((hull.LinkedTurret.Value == proj.Shooter.Value || hull.LinkedObserver == proj.Shooter) && proj.Shooter != null)
            args.Cancelled = true;
    }
    private void OnBeforeProjectileHit(EntityUid uid, ShiftTankBulletComponent component, ref ProjectileBeforeHitEvent args)
    {
        if (!TryComp<ShiftTankHullComponent>(args.Target, out var hull)) return;
        if (!hull.LinkedTurret.HasValue) return;
        if (!args.Shooter.HasValue) return;

        if (hull.LinkedTurret.Value == args.Shooter.Value)
            args.Cancelled = true;
    }
    private bool MovementUp(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (playerSession.AttachedEntity == null) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
            OnTankMove(playerSession.AttachedEntity.Value, comp, true);
        return false;
    }

    private void OnTankMove(EntityUid uid, ShiftTankHullComponent comp, bool forward)
    {
        if (forward && comp.MoveDirection == 0)
            comp.MoveDirection = 1;
        if (forward && comp.MoveDirection == -1)
            comp.MoveDirection = 0;
        if (!forward && comp.MoveDirection == 0)
            comp.MoveDirection = -1;
        if (!forward && comp.MoveDirection == 1)
            comp.MoveDirection = 0;

        if (comp.MoveDirection != 0)
            comp.IsMoving = true;
        else
            comp.IsMoving = false;
        Dirty(uid, comp); // Синхронизировать состояние с клиентом, если нужно
        if (TryComp<PhysicsComponent>(uid, out var physics) && comp.MoveDirection == 0)
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
    }
    private void OnTankRotate(EntityUid uid, ShiftTankHullComponent comp, bool right)
    {
        if (right && comp.RotationDirection == 0)
            comp.RotationDirection = 1;
        if (right && comp.RotationDirection == -1)
            comp.RotationDirection = 0;
        if (!right && comp.RotationDirection == 0)
            comp.RotationDirection = -1;
        if (!right && comp.RotationDirection == 1)
            comp.RotationDirection = 0;
        if (comp.RotationDirection != 0)
            comp.IsRotating = true;
        else
            comp.IsRotating = false;
        Dirty(uid, comp);
    }
    private bool MovementRight(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (playerSession.AttachedEntity == null) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
            OnTankRotate(playerSession.AttachedEntity.Value, comp, true);
        return false;
    }
    private bool MovementDown(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (playerSession.AttachedEntity == null) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
            OnTankMove(playerSession.AttachedEntity.Value, comp, false);
        return false;
    }
    private bool MovementLeft(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (playerSession.AttachedEntity == null) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
            OnTankRotate(playerSession.AttachedEntity.Value, comp, false);
        return false;
    }

}
