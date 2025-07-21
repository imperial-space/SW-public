using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Prayer;
using Content.Server.ShiftFront.Components;
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
using Content.Shared.ShiftFront.Components;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftFPVSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedActionsSystem _action = default!;
        [Dependency] private readonly SharedContentEyeSystem _eye = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftFPVControllerComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<ShiftFPVControllerComponent, UseInHandEvent>(OnActivate);
            SubscribeLocalEvent<ShiftFPVDroneComponent, StartCollideEvent>(OnCollide);
            SubscribeLocalEvent<ShiftFPVDroneComponent, DamageChangedEvent>(OnDamageDrone);
            SubscribeLocalEvent<ShiftFPVDroneComponent, FPVStopControlEvent>(OnStopAction);
            SubscribeLocalEvent<ShiftFPVDroneComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<ShiftFPVPilotComponent, DamageChangedEvent>(OnDamagePilot);
        }
        public void OnStart(EntityUid uid, ShiftFPVDroneComponent comp, ComponentStartup args)
        {
            _action.AddAction(uid, "StopFPVControll");
            if (HasComp<CombatModeComponent>(uid) && comp.Pacif) RemComp<CombatModeComponent>(uid);
        }
        private bool CheckMaskWearing(EntityUid uid)
        {
            if (!TryComp<InventoryComponent>(uid, out var inventoryComponent)) return false;
            var check1 = _inventorySystem.TryGetSlotEntity(uid, "head", out var slot1, inventoryComponent);
            if (!check1 || !HasComp<ShiftFPVMaskComponent>(slot1)) return false;
            return true;
        }
        private void OnStopAction(EntityUid uid, ShiftFPVDroneComponent comp, ref FPVStopControlEvent args)
        {
            StopControl(uid, comp, false);
        }
        private void OnDamagePilot(EntityUid uid, ShiftFPVPilotComponent comp, ref DamageChangedEvent args)
        {
            if (args.DamageDelta != null && args.DamageDelta.GetTotal() > 7f && TryComp<ShiftFPVDroneComponent>(comp.Drone, out var drone))
                StopControl(drone.Owner, drone, false);
        }
        private void OnDamageDrone(EntityUid uid, ShiftFPVDroneComponent comp, ref DamageChangedEvent args)
        {
            if (comp.Explosive)
                DroneExplode(uid, comp, false);
        }
        private void OnCollide(EntityUid uid, ShiftFPVDroneComponent comp, ref StartCollideEvent args)
        {
            if (TryComp<MobStateComponent>(args.OtherEntity, out var mob) && mob.CurrentState == MobState.Dead)
                return;
            if (comp.CMD)
                return;
            if (comp.Explosive)
            {
                if (TryComp<ShiftTankHullComponent>(args.OtherEntity, out var tank))
                    _damageableSystem.TryChangeDamage(args.OtherEntity, comp.Damage * tank.FPVResist);

            }
            DroneExplode(uid, comp, true);
        }
        public void DroneExplode(EntityUid uid, ShiftFPVDroneComponent comp, bool needPilot)
        {
            if (comp.Pilot == null && needPilot) return;
            StopControl(uid, comp, true);
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            if (!comp.CMD)
            {
                Spawn(comp.ExplosionEffect, coords);
                foreach (var target in _lookup.GetEntitiesInRange(coords, 4.5f))
                {
                    if (TryComp<ShiftPlayerComponent>(target, out var player))
                    {
                        player.Suppression -= 40f;
                        float zoom = 1f * (player.Suppression / 100f);
                        zoom = Math.Clamp(zoom, 0.4f, 1f);
                        player.Suppression = Math.Clamp(player.Suppression, player.SuppressionMin, player.SuppressionMax);
                        _eye.SetZoom(target, new Vector2(zoom, zoom));
                        _eye.SetMaxZoom(target, new Vector2(zoom, zoom));
                    }
                }
            }
            QueueDel(uid);
        }
        private void OnActivate(EntityUid uid, ShiftFPVControllerComponent comp, ref UseInHandEvent args)
        {
            args.Handled = true;
            if (!_sharedPlayerManager.TryGetSessionByEntity(args.User, out var session)) return;
            if (!_mind.TryGetMind(args.User, out _, out var mindcomp)) return;
            if (comp.LinkedDrone == null)
            {
                _prayerSystem.SendSubtleMessage(session, session, "В данный момент к контроллеру не привязан дрон", "Нет дрона");
                return;
            }

            if (!CheckMaskWearing(args.User) && comp.NeedVR && TryComp<ShiftPlayerComponent>(args.User, out var plr) && !plr.Leader)
            {
                _prayerSystem.SendSubtleMessage(session, session, "Оденьте VR очки для управления FPV дроном", "Нужны очки");
                return;
            }
            _prayerSystem.SendSubtleMessage(session, session, "Управление дроном запущено", "Управление");

            var drone = EnsureComp<ShiftFPVDroneComponent>(comp.LinkedDrone.Value);
            drone.Pilot = args.User;
            drone.Controller = uid;

            var pilot = EnsureComp<ShiftFPVPilotComponent>(args.User);
            pilot.Drone = comp.LinkedDrone;

            if (HasComp<GhostTakeoverAvailableComponent>(args.User)) RemComp<GhostTakeoverAvailableComponent>(args.User);
            if (HasComp<GhostRoleComponent>(args.User)) RemComp<GhostRoleComponent>(args.User);
            if (HasComp<AmbientSoundComponent>(drone.Owner)) _ambient.SetAmbience(drone.Owner, true);
            EnsureComp<UnremoveableComponent>(uid);
            _audio.PlayPvs(new SoundPathSpecifier(drone.EffectSoundOnStart), drone.Owner);
            _mind.TransferTo(mindcomp.Owner, drone.Owner, true, false, mindcomp);
        }
        public void StopControl(EntityUid uid, ShiftFPVDroneComponent comp, bool resetController)
        {
            if (TryComp<ShiftFPVControllerComponent>(comp.Controller, out var contoller) && resetController) contoller.LinkedDrone = null;
            if (HasComp<UnremoveableComponent>(comp.Controller)) RemComp<UnremoveableComponent>(comp.Controller.Value);
            if (comp.Pilot == null) return;
            if (!_mind.TryGetMind(uid, out _, out var mindcomp)) return;
            _mind.TransferTo(mindcomp.Owner, comp.Pilot.Value, true, false, mindcomp);
            if (HasComp<ShiftFPVPilotComponent>(comp.Pilot)) RemComp<ShiftFPVPilotComponent>(comp.Pilot.Value);
            if (!_sharedPlayerManager.TryGetSessionByEntity(comp.Pilot.Value, out var session)) return;
            comp.Pilot = null;
            _prayerSystem.SendSubtleMessage(session, session, "Управление FPV дроном прекращено", "Управление");
        }
        public void OnUseInHand(EntityUid uid, ShiftFPVControllerComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, ShiftFPVControllerComponent comp)
        {
            if (target == null)
                return;
            if (!_entityManager.EntityExists(target.Value))
                return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(user, out var session))
                return;
            if (TryComp<ShiftStructureComponent>(target.Value, out var struc) && TryComp<ShiftPlayerComponent>(user, out var player) && struc.Faction != player.Faction)
                return;

            if (TryComp<ShiftFPVDroneComponent>(target.Value, out var drone))
            {
                if (comp.LinkedDrone == target.Value)
                {
                    _prayerSystem.SendSubtleMessage(session, session, "Этот дрон уже привязан к вашему контроллеру", "Дрон уже ваш");
                    return;
                }

                if (drone.Controller != null)
                {
                    _prayerSystem.SendSubtleMessage(session, session, "Этот дрон уже привязан к какому-либо контроллеру", "Дрон уже привязан");
                    return;
                }

                if (comp.LinkedDrone.HasValue)
                {
                    var oldDroneUid = comp.LinkedDrone.Value;
                    if (_entityManager.EntityExists(oldDroneUid) && TryComp<ShiftFPVDroneComponent>(oldDroneUid, out var oldDroneComp))
                        oldDroneComp.Controller = null;
                }

                _prayerSystem.SendSubtleMessage(session, session, "Вы успешно привязали дрон к контроллеру", "Дрон привязан");
                _audio.PlayPvs(new SoundPathSpecifier(drone.EffectSoundOnLink), target.Value);
                comp.LinkedDrone = target.Value;
                drone.Controller = used;
            }
        }
    }
}
