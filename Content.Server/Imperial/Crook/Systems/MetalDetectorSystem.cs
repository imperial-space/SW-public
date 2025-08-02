using System.Linq;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Content.Shared.Access.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Imperial.Security;
using Content.Shared.Power;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;

namespace Content.Server.Imperial.Security
{
    public sealed class MetalDetectorSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
        [Dependency] private readonly SharedIdCardSystem _idCard = default!;
        [Dependency] private readonly EmagSystem _emag = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MetalDetectorComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<MetalDetectorComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<MetalDetectorComponent, GotEmaggedEvent>(OnEmagged);
        }

        private void OnPowerChanged(EntityUid uid, MetalDetectorComponent comp, ref PowerChangedEvent args)
        {
            comp.Powered = args.Powered;
            UpdateState(uid, comp.Powered ? MetalDetectorVisualState.Powered : MetalDetectorVisualState.Off, comp);
        }

        private void OnStartup(Entity<MetalDetectorComponent> detector, ref ComponentStartup args)
        {
            detector.Comp.NextStateReset = _timing.CurTime;
            UpdateState(detector.Owner, detector.Comp.Powered ? MetalDetectorVisualState.Powered : MetalDetectorVisualState.Off, detector.Comp);
        }

        private void OnEmagged(EntityUid uid, MetalDetectorComponent comp, ref GotEmaggedEvent args)
        {
            if (comp.Emagged || !_emag.CompareFlag(args.Type, EmagType.Access))
                return;

            comp.Emagged = true;
            args.Handled = true;
            args.Repeatable = false;

            UpdateState(uid, MetalDetectorVisualState.Scanning, comp);
            _audio.PlayPvs(comp.AlertSound, uid);
            EnsureComp<EmaggedComponent>(uid);
        }

        private void UpdateState(EntityUid uid, MetalDetectorVisualState state, MetalDetectorComponent comp)
        {
            comp.State = state;
            _appearance.SetData(uid, MetalDetectorVisuals.State, state);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<MetalDetectorComponent, PhysicsComponent>();
            while (query.MoveNext(out var uid, out var comp, out var phys))
            {
                if (!comp.Powered || !phys.CanCollide)
                    continue;

                if ((comp.State == MetalDetectorVisualState.Alert ||
                     comp.State == MetalDetectorVisualState.Warning ||
                     comp.State == MetalDetectorVisualState.Scanning) &&
                    _timing.CurTime > comp.NextStateReset)
                {
                    UpdateState(uid, MetalDetectorVisualState.Powered, comp);
                }

                ProcessDetection(uid, comp, phys);
            }
        }

        private void ProcessDetection(EntityUid uid, MetalDetectorComponent comp, PhysicsComponent phys)
        {
            var intersecting = _physics.GetContactingEntities(uid, phys);
            if (intersecting == null)
                return;

            foreach (var entity in intersecting)
            {
                if (!HasComp<InventoryComponent>(entity) ||
                    comp.ScannedEntities.TryGetValue(entity, out var lastScan) &&
                    _timing.CurTime < lastScan + comp.ScanCooldown)
                {
                    continue;
                }

                comp.ScannedEntities[entity] = _timing.CurTime;
                ProcessEntity(uid, entity, comp);
            }
        }

        private void ProcessEntity(EntityUid detector, EntityUid entity, MetalDetectorComponent comp)
        {
            if (comp.Emagged || HasRequiredAccess(entity, comp))
            {
                UpdateState(detector, MetalDetectorVisualState.Scanning, comp);
                _audio.PlayPvs(comp.ClearSound, detector);
                comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
                return;
            }

            var (hasWeaponAndContraband, hasContraband) = CheckEntity(entity, comp);

            if (hasWeaponAndContraband && comp.CheckWeapons && comp.CheckContraband)
            {
                UpdateState(detector, MetalDetectorVisualState.Alert, comp);
                _audio.PlayPvs(comp.AlertSound, detector);
                comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
                return;
            }
            else if (hasContraband && comp.CheckContraband)
            {
                UpdateState(detector, MetalDetectorVisualState.Warning, comp);
                _audio.PlayPvs(comp.WarningSound, detector);
                comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
                return;
            }
            UpdateState(detector, MetalDetectorVisualState.Scanning, comp);
            _audio.PlayPvs(comp.ClearSound, detector);
            comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
        }

        private (bool hasWeaponAndContraband, bool hasContraband) CheckEntity(EntityUid entity, MetalDetectorComponent comp)
        {
            bool weaponAndContraband = false;
            bool contrabandOnly = false;
            foreach (var slot in comp.CheckedSlots)
            {
                if (_inventory.TryGetSlotEntity(entity, slot, out var item))
                {
                    CheckItem(item.Value, ref weaponAndContraband, ref contrabandOnly);
                }
            }
            foreach (var heldItem in _hands.EnumerateHeld(entity))
            {
                CheckItem(heldItem, ref weaponAndContraband, ref contrabandOnly);
            }

            return (weaponAndContraband, contrabandOnly);
        }

        private void CheckItem(EntityUid item, ref bool hasWeaponAndContraband, ref bool hasContrabandOnly)
        {
            var hasWeapon = HasComp<GunComponent>(item) || HasComp<MeleeWeaponComponent>(item);
            var hasContraband = HasComp<ContrabandComponent>(item);

            if (!hasWeaponAndContraband && hasWeapon && hasContraband)
            {
                hasWeaponAndContraband = true;
            }
            else if (!hasContrabandOnly && hasContraband)
            {
                hasContrabandOnly = true;
            }
        }

        private bool HasRequiredAccess(EntityUid userUid, MetalDetectorComponent detector)
        {
            if (TryComp<AccessComponent>(userUid, out var access) &&
                detector.AllowedAccess.Any(required => access.Tags.Contains(required)))
            {
                return true;
            }

            if (!_idCard.TryFindIdCard(userUid, out var idCard))
                return false;

            return TryComp<AccessComponent>(idCard, out var idAccess) &&
                   detector.AllowedAccess.Any(required => idAccess.Tags.Contains(required));
        }
    }
}
