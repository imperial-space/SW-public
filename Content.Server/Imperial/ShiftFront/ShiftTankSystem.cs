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
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Spawners;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Damage;
using Content.Server.Mind;
using Content.Shared.Interaction.Events;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Physics.Events;
using Content.Shared.Audio;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.FPV;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Interaction.Components;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Radio.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Movement.Pulling.Components;

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
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly SharedEyeSystem _eye = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly ChatSystem _chat = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftTankHullComponent, ComponentStartup>(TankStart);
            SubscribeLocalEvent<ShiftTankHullComponent, ComponentShutdown>(TankEnd);
            SubscribeLocalEvent<ShiftTankTurretComponent, ComponentStartup>(TurretStart);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStartMoveEvent>(OnTankStartMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankStopMoveEvent>(OnTankStopMove);
            SubscribeLocalEvent<ShiftTankHullComponent, TankChangeMoveDirectionEvent>(OnChangeMoveDirection);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotateEvent>(OnToggleRotate);
            SubscribeLocalEvent<ShiftTankHullComponent, TankToggleRotationDirectionEvent>(OnToggleRotationDirection);
            SubscribeLocalEvent<ShiftTankHullComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ShiftTankpartComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<ShiftTankHullComponent, ActivateInWorldEvent>(OnActivateTank);
            SubscribeLocalEvent<ShiftPlayerComponent, EntitySpokeEvent>(OnSpeak);

        }

        private void OnSpeak(EntityUid uid, ShiftPlayerComponent comp, EntitySpokeEvent args)
        {
            if (comp.Vehicle == null) return;
            _chat.TrySendInGameICMessage(comp.Vehicle.Value, args.Message, InGameICChatType.Whisper, false, nameOverride: "голос изнутри техники");
        }
        private void TankEnd(EntityUid uid, ShiftTankHullComponent comp, ref ComponentShutdown args)
        {
            //if (comp.LinkedGrid != null) QueueDel(comp.LinkedGrid.Value);
            if (comp.LinkedTurret != null) QueueDel(comp.LinkedTurret.Value);
            if (comp.LinkedObserver != null) QueueDel(comp.LinkedObserver.Value);
            if (comp.InsideControllerEntity != null) Spawn("MedievalExplodeAp2", Transform(comp.InsideControllerEntity.Value).Coordinates);
        }
        private void OnActivateTank(EntityUid uid, ShiftTankHullComponent comp, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;
            if (!HasComp<ShiftPlayerComponent>(args.User) && !HasComp<BypassInteractionChecksComponent>(args.User)) return;
            if (TryComp<ShiftPlayerComponent>(args.User, out var player) && player.Faction != comp.Faction) return;
            if (comp.InsideEntryEntity == null) return;
            var xform = Transform(comp.InsideEntryEntity.Value);
            var coords = xform.Coordinates;
            if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling != null)
            {
                _transform.SetCoordinates(puller.Pulling.Value, coords);
            }
            _transform.SetCoordinates(args.User, coords);
            if (player != null)
                player.Vehicle = uid;
        }
        public void TurretStart(EntityUid uid, ShiftTankTurretComponent component, ComponentStartup args)
        {
            //_action.AddAction(uid, "StopFPVControll");
        }

        private void OnActivate(EntityUid uid, ShiftTankpartComponent comp, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;
            if (comp.Tank == null) return;

            var tank = EnsureComp<ShiftTankHullComponent>(comp.Tank.Value);
            switch (comp.Part)
            {
                case "Controller":
                    break;
                case "Gunner":
                    break;
                case "Exit":
                    var xform = Transform(comp.Tank.Value);
                    var coords = xform.Coordinates;
                    if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling != null)
                    {
                        _transform.SetCoordinates(puller.Pulling.Value, coords);
                    }
                    _transform.SetCoordinates(args.User, coords);
                    if (TryComp<ShiftPlayerComponent>(args.User, out var player))
                        player.Vehicle = null;
                    break;
                case "Observer":
                    break;
            }

        }

        private void OnExamine(EntityUid uid, ShiftTankHullComponent comp, ExaminedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > 0)
                args.PushMarkup($"У техники [color=red]{Math.Round(dam.TotalDamage.Float(), 2)}[/color] единиц  повреждений", 5);
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
            if (component.TurretProto != "")
            {
                var turret = Spawn(component.TurretProto, Transform(uid).Coordinates);
                _transform.SetParent(turret, uid);
                EnsureComp<ShiftTankTurretComponent>(turret, out var turretcomp);
                turretcomp.LinkedTank = uid;
                component.LinkedTurret = turret;
            }
            if (component.ObserverProto != "")
            {
                var observ = Spawn(component.ObserverProto, Transform(uid).Coordinates);
                _transform.SetParent(observ, uid);
                component.LinkedObserver = observ;
                EnsureComp<ShiftTankTurretComponent>(observ, out var turretcomp2);
                turretcomp2.LinkedTank = uid;
            }
            var map = SpawnMap(component.GridLink);
            if (!map.HasValue) return;
            component.LinkedGrid = map.Value.Owner;
            var query = EntityQueryEnumerator<ShiftInsideMarkerComponent>();
            while (query.MoveNext(out var insuid, out var inside))
            {
                switch (inside.Inside)
                {
                    case "Controller":
                        if (component.InsideController == "") continue;
                        component.InsideControllerEntity = Spawn(component.InsideController, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn);
                        EnsureComp<ShiftTankpartComponent>(component.InsideControllerEntity.Value, out var controller);
                        EnsureComp<ShiftFPVControllerComponent>(component.InsideControllerEntity.Value, out var control);
                        control.TankPart = true;
                        control.LinkedDrone = uid;
                        EnsureComp<ShiftFPVDroneComponent>(uid, out var drone);
                        drone.Controller = component.InsideControllerEntity.Value;
                        drone.Explosive = false;
                        drone.Pacif = false;
                        drone.TankPart = true;
                        controller.Tank = uid;
                        despawn.Lifetime = 0.05f;
                        break;
                    case "Gunner":
                        if (component.InsideGunner == "") continue;
                        component.InsideGunnerEntity = Spawn(component.InsideGunner, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn2);
                        EnsureComp<ShiftTankpartComponent>(component.InsideGunnerEntity.Value, out var gunner);
                        EnsureComp<ShiftFPVControllerComponent>(component.InsideGunnerEntity.Value, out var control2);
                        control2.TankPart = true;
                        if (component.LinkedTurret == null) continue;
                        control2.LinkedDrone = component.LinkedTurret.Value;
                        EnsureComp<ShiftFPVDroneComponent>(component.LinkedTurret.Value, out var drone2);
                        drone2.Controller = component.InsideGunnerEntity.Value;
                        drone2.Explosive = false;
                        drone2.Pacif = false;
                        drone2.TankPart = true;
                        gunner.Tank = uid;
                        despawn2.Lifetime = 0.05f;
                        break;
                    case "Cartridge":
                        if (component.InsideCartridge == "") continue;
                        component.InsideCartridgeEntity = Spawn(component.InsideCartridge, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn3);
                        EnsureComp<ShiftTankpartComponent>(component.InsideCartridgeEntity.Value, out var cartridge);
                        cartridge.Tank = uid;
                        despawn3.Lifetime = 0.05f;
                        break;
                    case "Exit":
                        if (component.InsideExit == "") continue;
                        component.InsideExitEntity = Spawn(component.InsideExit, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn4);
                        EnsureComp<ShiftTankpartComponent>(component.InsideExitEntity.Value, out var exit);
                        exit.Tank = uid;
                        despawn4.Lifetime = 0.05f;
                        break;
                    case "Entry":
                        if (component.InsideEntry == "") continue;
                        component.InsideEntryEntity = Spawn(component.InsideEntry, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn5);
                        EnsureComp<ShiftTankpartComponent>(component.InsideEntryEntity.Value, out var entry);
                        entry.Tank = uid;
                        despawn5.Lifetime = 0.05f;
                        break;
                    case "Motor":
                        if (component.InsideMotor == "") continue;
                        component.InsideMotorEntity = Spawn(component.InsideMotor, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn6);
                        EnsureComp<ShiftTankpartComponent>(component.InsideMotorEntity.Value, out var motor);
                        motor.Tank = uid;
                        despawn6.Lifetime = 0.05f;
                        break;
                    case "Observer":
                        if (component.InsideObserver == "") continue;
                        component.InsideObserverEntity = Spawn(component.InsideObserver, Transform(insuid).Coordinates);
                        EnsureComp<TimedDespawnComponent>(insuid, out var despawn7);
                        EnsureComp<ShiftTankpartComponent>(component.InsideObserverEntity.Value, out var observer);
                        observer.Tank = uid;
                        despawn7.Lifetime = 0.05f;
                        EnsureComp<ShiftFPVControllerComponent>(component.InsideObserverEntity.Value, out var control3);
                        if (component.InsideObserverEntity == null) continue;
                        if (component.LinkedObserver == null) continue;
                        control3.LinkedDrone = component.LinkedObserver.Value;
                        control3.TankPart = true;
                        EnsureComp<ShiftFPVDroneComponent>(component.LinkedObserver.Value, out var drone3);
                        drone3.Controller = component.InsideObserverEntity.Value;
                        drone3.Explosive = false;
                        drone3.Pacif = false;
                        drone3.TankPart = true;
                        break;
                }
            }
        }
        private Entity<Robust.Shared.Map.Components.MapComponent>? SpawnMap(ResPath[] mappath)
        {
            var path = _random.Pick(mappath);
            var options = new DeserializationOptions
            {
                InitializeMaps = true,
            };

            if (_mapLoaderSystem.TryLoadMap(path, out var first, out var second, options))
                return first;
            return null;
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
                    if (tankComp.NeedMoveForRotating && !tankComp.IsMoving)
                        continue;
                    // Устанавливаем угловую скорость
                    if (tankComp.MoveDirection > 0)
                        _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(uid, xform, MetaData(uid)), coords, Transform(uid).LocalRotation - tankComp.RotationDirection * tankComp.TurnRate);
                    else
                        _transform.SetCoordinates(new Entity<TransformComponent, MetaDataComponent>(uid, xform, MetaData(uid)), coords, Transform(uid).LocalRotation + tankComp.RotationDirection * tankComp.TurnRate);
                }
                else
                {
                }
            }
        }
    }
}
