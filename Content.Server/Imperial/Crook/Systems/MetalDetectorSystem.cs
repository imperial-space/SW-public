using System.Linq;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio;
using Content.Shared.Access.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Imperial.Security;

namespace Content.Server.Imperial.Security
{
    public sealed class MetalDetectorSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MetalDetectorComponent, ComponentStartup>(OnStartup);
        }

        private void OnStartup(Entity<MetalDetectorComponent> detector, ref ComponentStartup args)
        {
            detector.Comp.NextStateReset = _timing.CurTime;
            detector.Comp.State = MetalDetectorVisualState.Powered;
            _appearance.SetData(detector, MetalDetectorVisuals.State, detector.Comp.State);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<MetalDetectorComponent, PhysicsComponent>();
            while (query.MoveNext(out var uid, out var comp, out var phys))
            {
                if (!phys.CanCollide || Terminating(uid))
                    continue;

                ProcessDetector(uid, comp, phys);
            }
        }

        private void ProcessDetector(EntityUid uid, MetalDetectorComponent comp, PhysicsComponent phys)
        {
            var contacting = _physics.GetContactingEntities(uid, phys);
            if (contacting == null)
                return;

            foreach (var entity in contacting)
            {
                if (!IsValidTarget(entity))
                    continue;

                ProcessDetection(uid, entity, comp);
            }

            if (comp.State != MetalDetectorVisualState.Powered &&
                _timing.CurTime > comp.NextStateReset)
            {
                comp.State = MetalDetectorVisualState.Powered;
                _appearance.SetData(uid, MetalDetectorVisuals.State, comp.State);
            }
        }

        private bool IsValidTarget(EntityUid entity)
        {
            return !Terminating(entity) &&
                   HasComp<ActorComponent>(entity) &&
                   TryComp<PhysicsComponent>(entity, out var physics) &&
                   physics.CanCollide;
        }

        private void ProcessDetection(EntityUid detector, EntityUid entity, MetalDetectorComponent comp)
        {
            if (comp.ScannedPlayers.TryGetValue(entity, out var lastScan) &&
                _timing.CurTime < lastScan + comp.ScanCooldown)
            {
                return;
            }

            comp.ScannedPlayers[entity] = _timing.CurTime;

            if (HasRequiredAccess(entity, comp))
            {
                SetDetectorState(detector, MetalDetectorVisualState.Scanning, comp.ClearSound, comp);
                return;
            }

            var hasWeapon = HasWeapon(entity);
            SetDetectorState(detector,
                hasWeapon ? MetalDetectorVisualState.Alert : MetalDetectorVisualState.Scanning,
                hasWeapon ? comp.AlertSound : comp.ClearSound,
                comp);
        }

        private bool HasRequiredAccess(EntityUid userUid, MetalDetectorComponent detector)
        {
            if (!TryComp<AccessComponent>(userUid, out var access) || access.Tags == null)
                return false;

            foreach (var requiredTag in detector.AllowedAccess)
            {
                if (access.Tags.Contains(requiredTag))
                    return true;
            }

            return false;
        }

        private void SetDetectorState(EntityUid uid, MetalDetectorVisualState state,
            SoundSpecifier sound, MetalDetectorComponent component)
        {
            component.State = state;
            component.NextStateReset = _timing.CurTime + component.StateResetDelay;
            _audio.PlayPvs(sound, uid);
            _appearance.SetData(uid, MetalDetectorVisuals.State, state);
        }

        private bool HasWeapon(EntityUid entity)
        {
            if (Terminating(entity))
                return false;

            foreach (var container in _container.GetAllContainers(entity))
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (Terminating(item))
                        continue;

                    if (HasComp<GunComponent>(item) || HasComp<MeleeWeaponComponent>(item))
                        return true;
                }
            }
            return false;
        }
    }
}
