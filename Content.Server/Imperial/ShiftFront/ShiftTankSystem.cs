using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Siege.Events;
using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Physics.Components; // Нужно для управления физикой
using Robust.Shared.Physics.Systems;   // Нужно для управления физикой
using Content.Shared.ShiftFront.Components; // Остальные зависимости оставлены как были
using Content.Shared.Actions;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftTankSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedActionsSystem _action = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftTankHullComponent, ComponentStartup>(TankStart);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStartMoveEvent>(OnTankStartMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStopMoveEvent>(OnTankStopMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankChangeMoveDirectionEvent>(OnChangeMoveDirection);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotateEvent>(OnToggleRotate);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotationDirectionEvent>(OnToggleRotationDirection);
            SubscribeLocalEvent<ShiftTankHullComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, ShiftTankHullComponent comp, ExaminedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > 0)
                args.PushMarkup($"У техники [color=red]{Math.Round(dam.TotalDamage.Float(), 2)}[/color] единиц  повреждений, подъедьте к мед. вышке ремонта", 5);
        }
        public void TankStart(EntityUid uid, ShiftTankHullComponent component, ComponentStartup args)
        {
            //_action.AddAction(uid, "TankStartMoveAction");
            //_action.AddAction(uid, "TankStopMoveAction");
            //_action.AddAction(uid, "TankChangeMoveDirectionAction");
            //_action.AddAction(uid, "TankToggleRotateAction");
            //_action.AddAction(uid, "TankToggleRotationDirectionAction");
            EnsureComp<PhysicsComponent>(uid);
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                _physics.SetFixedRotation(uid, false, body: physics);
            }
            if (component.TurretProto != null)
            {
                var turret = Spawn(component.TurretProto, Transform(uid).Coordinates);
                _transform.SetParent(turret, uid);
                EnsureComp<ShiftTankTurretComponent>(turret, out var turretcomp);
                turretcomp.LinkedTank = uid;
                component.LinkedTurret = turret;
            }
        }

        private void OnTankShutdown(EntityUid uid, ShiftTankHullComponent component, ComponentShutdown args)
        {
            // Можно добавить другую логику очистки, если нужно
        }

        private void OnTankStartMove(EntityUid uid, ShiftTankHullComponent component, ref TankStartMoveEvent args)
        {
            if (component.IsMoving) return; // Уже движется

            component.IsMoving = true;
            Dirty(uid, component); // Синхронизировать состояние с клиентом, если нужно
        }

        private void OnTankStopMove(EntityUid uid, ShiftTankHullComponent component, ref TankStopMoveEvent args)
        {
            if (!component.IsMoving) return; // Уже остановлен

            component.IsMoving = false;
            //Log.Debug($"Tank {ToPrettyString(uid)} stopped moving.");

            // Явно останавливаем движение в физике
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
            }
            Dirty(uid, component);
        }

        private void OnChangeMoveDirection(EntityUid uid, ShiftTankHullComponent component, ref TankChangeMoveDirectionEvent args)
        {
            component.MoveDirection *= -1; // Инвертируем направление
            //Log.Debug($"Tank {ToPrettyString(uid)} changed move direction to {component.MoveDirection}.");
            Dirty(uid, component);
            // Если танк движется, смена направления должна сразу повлиять на вектор скорости в Update
        }

        private void OnToggleRotate(EntityUid uid, ShiftTankHullComponent component, ref TankToggleRotateEvent args)
        {
            component.IsRotating = !component.IsRotating;
            Dirty(uid, component);
        }

        private void OnToggleRotationDirection(EntityUid uid, ShiftTankHullComponent component, ref TankToggleRotationDirectionEvent args)
        {
            component.RotationDirection *= -1; // Инвертируем направление вращения
            //Log.Debug($"Tank {ToPrettyString(uid)} toggled rotation direction to {component.RotationDirection}.");
            Dirty(uid, component);
            // Если танк вращается, смена направления должна сразу повлиять на угловую скорость в Update
        }

        // --- Логика непрерывного движения и вращения ---
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Создаем запрос для всех активных танков с физическим телом
            var query = EntityQueryEnumerator<ShiftTankHullComponent, PhysicsComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var tankComp, out var physicsComp, out var xform))
            {
                // Пропускаем танки на паузе или без физического тела/трансформа
                if (Paused(uid) || xform.MapUid == null)
                {
                    continue;
                }
                // --- Обработка линейного движения ---
                if (tankComp.IsMoving)
                {
                    var baseForwardVector = -Vector2.UnitY;
                    var currentRotation = xform.LocalRotation;
                    var actualForwardVector = currentRotation.RotateVec(baseForwardVector);
                    var targetVelocity = actualForwardVector * tankComp.MoveSpeed * tankComp.MoveDirection;
                    if (tankComp.MoveDirection > 0)
                        _physics.SetLinearVelocity(uid, targetVelocity, body: physicsComp);
                    else
                        _physics.SetLinearVelocity(uid, targetVelocity * tankComp.BackMoveModifier, body: physicsComp);
                }
                else
                {
                }
                var coords = xform.Coordinates;

                // --- Обработка вращения ---
                if (tankComp.IsRotating)
                {
                    // Устанавливаем угловую скорость
                    _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(uid, xform, MetaData(uid)), coords, Transform(uid).LocalRotation - tankComp.RotationDirection * tankComp.TurnRate);
                }
                else
                {
                }
            }
        }
    }
}
