using System.Linq;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Power;
using Content.Shared.Access.Components;
using Content.Shared.Weapons.Melee;
using Content.Server.Imperial.Crook.Components;
using Content.Shared.Imperial.Crook.Visuals;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Crook.Systems
{
    public sealed class MetalDetectorSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedIdCardSystem _idCard = default!;
        [Dependency] private readonly EmagSystem _emag = default!;

        private const float ThinkRate = 0.25f;
        private float _accumulatedTime;
        private readonly Dictionary<EntityUid, (bool hasWeaponAndContraband, bool hasContrabandOnly)> _cachedResults = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MetalDetectorComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<MetalDetectorComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<MetalDetectorComponent, GotEmaggedEvent>(OnEmagged);
            SubscribeLocalEvent<MetalDetectorComponent, StartCollideEvent>(OnStartCollide);
            SubscribeLocalEvent<MetalDetectorComponent, EndCollideEvent>(OnEndCollide);
            SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnInventoryChanged);
            SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnInventoryChanged);
        }

        private void OnInventoryChanged(EntInsertedIntoContainerMessage args)
        {
            if (HasComp<InventoryComponent>(args.Container.Owner))
                _cachedResults.Remove(args.Container.Owner);
        }

        private void OnInventoryChanged(EntRemovedFromContainerMessage args)
        {
            if (HasComp<InventoryComponent>(args.Container.Owner))
                _cachedResults.Remove(args.Container.Owner);
        }

        private void OnStartCollide(EntityUid uid, MetalDetectorComponent comp, ref StartCollideEvent args)
        {
            if (!comp.Powered || !args.OtherBody.CanCollide)
                return;

            var otherEntity = args.OtherEntity;

            if (!HasComp<InventoryComponent>(otherEntity))
                return;

            if (!comp.CollidingEntities.Add(otherEntity))
                return;

            ProcessEntity(uid, otherEntity, comp);
        }

        private void OnEndCollide(EntityUid uid, MetalDetectorComponent comp, ref EndCollideEvent args)
        {
            comp.CollidingEntities.Remove(args.OtherEntity);

            if (comp.CollidingEntities.Count == 0 && comp.State != MetalDetectorVisualState.Powered)
            {
                UpdateState(uid, MetalDetectorVisualState.Powered, comp);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _accumulatedTime += frameTime;
            if (_accumulatedTime < ThinkRate)
                return;

            _accumulatedTime -= ThinkRate;

            var query = EntityQueryEnumerator<MetalDetectorComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (!comp.Powered)
                    continue;

                if (comp.State == MetalDetectorVisualState.Powered)
                    continue;

                if (_timing.CurTime > comp.NextStateReset)
                {
                    if (comp.CollidingEntities.Count > 0)
                    {
                        comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
                        foreach (var entity in comp.CollidingEntities)
                        {
                            if (Exists(entity))
                                ProcessEntity(uid, entity, comp);
                        }
                    }
                    else
                    {
                        UpdateState(uid, MetalDetectorVisualState.Powered, comp);
                    }
                }
            }
        }

        private void OnPowerChanged(EntityUid uid, MetalDetectorComponent comp, ref PowerChangedEvent args)
        {
            comp.Powered = args.Powered;
            UpdateState(uid, comp.Powered ? MetalDetectorVisualState.Powered : MetalDetectorVisualState.Off, comp);

            if (!comp.Powered)
                comp.CollidingEntities.Clear();
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
            if (comp.State == state)
                return;

            comp.State = state;
            _appearance.SetData(uid, MetalDetectorVisuals.State, state);
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

            if (!_cachedResults.TryGetValue(entity, out var result))
            {
                result = CheckEntity(entity, comp);
                _cachedResults[entity] = result;
            }

            var (hasWeaponAndContraband, hasContrabandOnly) = result;

            if (hasWeaponAndContraband && comp.CheckWeapons && comp.CheckContraband)
            {
                UpdateState(detector, MetalDetectorVisualState.Alert, comp);
                _audio.PlayPvs(comp.AlertSound, detector);
            }
            else if (hasContrabandOnly && comp.CheckContraband)
            {
                UpdateState(detector, MetalDetectorVisualState.Warning, comp);
                _audio.PlayPvs(comp.WarningSound, detector);
            }
            else
            {
                UpdateState(detector, MetalDetectorVisualState.Scanning, comp);
                _audio.PlayPvs(comp.ClearSound, detector);
            }

            comp.NextStateReset = _timing.CurTime + comp.StateResetDelay;
        }

        private (bool hasWeaponAndContraband, bool hasContrabandOnly) CheckEntity(EntityUid entity, MetalDetectorComponent comp)
        {
            bool weaponAndContraband = false;
            bool contrabandOnly = false;

            if (TryComp<InventoryComponent>(entity, out var inventory))
            {
                foreach (var slot in inventory.Slots)
                {
                    if (_inventory.TryGetSlotEntity(entity, slot.Name, out var item) && item != null)
                    {
                        CheckItemAndContainers(item.Value, ref weaponAndContraband, ref contrabandOnly, 0, comp.MaxRecursionDepth);
                        if (weaponAndContraband)
                            return (true, true);
                    }
                }
            }

            foreach (var heldItem in _hands.EnumerateHeld(entity))
            {
                if (!Exists(heldItem))
                    continue;

                CheckItem(heldItem, ref weaponAndContraband, ref contrabandOnly);
                if (weaponAndContraband)
                    break;
            }

            return (weaponAndContraband, contrabandOnly);
        }

        private void CheckItemAndContainers(EntityUid item, ref bool weaponAndContraband, ref bool contrabandOnly, int depth, int maxDepth)
        {
            if (depth > maxDepth)
                return;

            CheckItem(item, ref weaponAndContraband, ref contrabandOnly);
            if (weaponAndContraband)
                return;

            if (TryComp<ContainerManagerComponent>(item, out var containerManager))
            {
                foreach (var container in containerManager.Containers.Values)
                {
                    foreach (var containedItem in container.ContainedEntities)
                    {
                        if (!Exists(containedItem))
                            continue;

                        CheckItemAndContainers(containedItem, ref weaponAndContraband, ref contrabandOnly, depth + 1, maxDepth);
                        if (weaponAndContraband)
                            return;
                    }
                }
            }
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
