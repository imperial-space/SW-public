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
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.ShiftFront.Components;
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
            .Bind(EngineKeyFunctions.MoveUp, new PointerStateInputCmdHandler(MovementUpPressed, MovementUpReleased))
            .Bind(EngineKeyFunctions.MoveRight, new PointerStateInputCmdHandler(MovementRightPressed, MovementRightReleased))
            .Bind(EngineKeyFunctions.MoveDown, new PointerStateInputCmdHandler(MovementDownPressed, MovementDownReleased))
            .Bind(EngineKeyFunctions.MoveLeft, new PointerStateInputCmdHandler(MovementLeftPressed, MovementLeftReleased))
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

    private bool MovementUpPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            comp.MoveDirection = 1;
            comp.IsMoving = true;
            Dirty(player, comp);
        }
        return false;
    }

    private bool MovementUpReleased(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            // Останавливаем движение только если не нажата противоположная клавиша
            if (comp.MoveDirection == 1)
            {
                comp.MoveDirection = 0;
                comp.IsMoving = false;
                Dirty(player, comp);
                if (TryComp<PhysicsComponent>(player, out var physics))
                    _physics.SetLinearVelocity(player, Vector2.Zero, body: physics);
            }
        }
        return false;
    }

    private bool MovementRightPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            comp.RotationDirection = 1;
            comp.IsRotating = true;
            Dirty(player, comp);
        }
        return false;
    }

    private bool MovementRightReleased(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            // Останавливаем вращение только если не нажата противоположная клавиша
            if (comp.RotationDirection == 1)
            {
                comp.RotationDirection = 0;
                comp.IsRotating = false;
                Dirty(player, comp);
            }
        }
        return false;
    }

    private bool MovementDownPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            comp.MoveDirection = -1;
            comp.IsMoving = true;
            Dirty(player, comp);
        }
        return false;
    }

    private bool MovementDownReleased(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            // Останавливаем движение только если не нажата противоположная клавиша
            if (comp.MoveDirection == -1)
            {
                comp.MoveDirection = 0;
                comp.IsMoving = false;
                Dirty(player, comp);
                if (TryComp<PhysicsComponent>(player, out var physics))
                    _physics.SetLinearVelocity(player, Vector2.Zero, body: physics);
            }
        }
        return false;
    }

    private bool MovementLeftPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            comp.RotationDirection = -1;
            comp.IsRotating = true;
            Dirty(player, comp);
        }
        return false;
    }

    private bool MovementLeftReleased(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid uid)
    {
        if (!_timing.IsFirstTimePredicted) return false;
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player)) return false;
        if (TryComp<ShiftTankHullComponent>(player, out var comp))
        {
            // Останавливаем вращение только если не нажата противоположная клавиша
            if (comp.RotationDirection == -1)
            {
                comp.RotationDirection = 0;
                comp.IsRotating = false;
                Dirty(player, comp);
            }
        }
        return false;
    }
}
