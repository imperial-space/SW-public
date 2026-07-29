using Content.Shared.Containers.ItemSlots;
using Content.Shared.Doors.Systems;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

public sealed class UniversalLockableSharedSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockableComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem), typeof(SharedStorageSystem) });
        SubscribeLocalEvent<UniversalLockableComponent, StorageCloseAttemptEvent>(OnStorageCloseAttempt);
        SubscribeLocalEvent<UniversalLockableComponent, StorageInteractUsingAttemptEvent>(OnStorageInteractUsingAttemptEvent);
    }

    private void OnActivate(Entity<UniversalLockableComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (IsLocked(entity))
        {
            if (_net.IsServer)
            {
                var audioParams = new AudioParams { Volume = -10 };
                _audioSystem.PlayPvs(entity.Comp.ActivateInWorldDenySound, entity, audioParams);
            }

            args.Handled = true;
        }
    }

    private void OnStorageInteractUsingAttemptEvent(Entity<UniversalLockableComponent> entity, ref StorageInteractUsingAttemptEvent args)
    {
        if (IsLocked(entity))
            args.Cancelled = true;
    }

    private void OnStorageCloseAttempt(EntityUid uid, UniversalLockableComponent component, ref StorageCloseAttemptEvent args)
    {
        var lockableEntity = (uid, component);

        if (!IsLocked(lockableEntity))
            return;

        args.Cancelled = true;
    }

    private bool IsLocked(Entity<UniversalLockableComponent> entity)
    {
        if (!_itemSlots.TryGetSlot(entity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return false;

        return TryComp<UniversalLockComponent>(lockUid, out var lockComp) && lockComp.IsLocked;
    }
}
