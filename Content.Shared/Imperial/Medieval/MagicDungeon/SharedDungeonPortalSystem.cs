using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.MagicDungeon;

public abstract partial class SharedDungeonPortalSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _pointLightSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DungeonPortalFrameComponent, EntInsertedIntoContainerMessage>(OnShardInserted);
        SubscribeLocalEvent<DungeonPortalFrameComponent, ContainerIsInsertingAttemptEvent>(OnShardInsertAttempt);
        SubscribeLocalEvent<DungeonPortalFrameComponent, ComponentInit>(OnPortalInit);
        SubscribeLocalEvent<DungeonShardComponent, MapInitEvent>(OnMapInit);

        InitEscape();
    }

    private void OnMapInit(Entity<DungeonShardComponent> shard, ref MapInitEvent args)
    {
        var x = _robustRandom.Next(-1, 3);
        var y = _robustRandom.Next(-1, 3);
        shard.Comp.SizeModifier = (x, y);
        Dirty(shard);
    }

    private void OnPortalInit(EntityUid uid, DungeonPortalFrameComponent component, ComponentInit args)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        if (_itemSlots.TryGetSlot(uid, component.ShardSlotId1, out var slot1, itemSlots))
            component.ShardSlot1 = slot1;
        else
            _itemSlots.AddItemSlot(uid, component.ShardSlotId1, component.ShardSlot1, itemSlots);

        if (_itemSlots.TryGetSlot(uid, component.ShardSlotId2, out var slot2, itemSlots))
            component.ShardSlot2 = slot2;
        else
            _itemSlots.AddItemSlot(uid, component.ShardSlotId2, component.ShardSlot2, itemSlots);
    }

    private void OnShardInsertAttempt(Entity<DungeonPortalFrameComponent> portalFrame, ref ContainerIsInsertingAttemptEvent args)
    {
        if (portalFrame.Comp.IsActive)
            return;

        args.Cancel();
    }

    private void OnShardInserted(Entity<DungeonPortalFrameComponent> portalFrame, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != portalFrame.Comp.ShardSlotId1 && args.Container.ID != portalFrame.Comp.ShardSlotId2)
            return;

        if (!TryComp<ItemSlotsComponent>(portalFrame, out var itemSlots))
            return;

        if (_itemSlots.TryGetSlot(portalFrame, args.Container.ID, out var shardSlot, itemSlots) || shardSlot == null)
            return;

        var shard = args.Entity;
        var shardComp = EnsureComp<DungeonShardComponent>(shard);
        portalFrame.Comp.BaseSize += shardComp.SizeModifier;
        Dirty(portalFrame);

        _itemSlots.SetLock(portalFrame, shardSlot, true, itemSlots);
        ChangeShardHolesAppearance(portalFrame);
        if (!_itemSlots.CanInsert(portalFrame, shard, null, portalFrame.Comp.ShardSlot1) && !_itemSlots.CanInsert(portalFrame, shard, null, portalFrame.Comp.ShardSlot2))
        {
            OpenPortal(portalFrame);
        }
    }

    private void OpenPortal(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        SpawnPortal(portalFrame);
        portalFrame.Comp.IsOpened = true;
        ChangePortalAppearance(portalFrame);
        Dirty(portalFrame);
    }

    private void ClosePortal(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        portalFrame.Comp.IsOpened = false;
        ChangePortalAppearance(portalFrame);
    }

    private void DeactivatePortal(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        portalFrame.Comp.IsActive = false;
        portalFrame.Comp.IsOpened = false;
        ChangePortalAppearance(portalFrame);
    }

    protected virtual void SpawnPortal(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        portalFrame.Comp.IsActive = true;
    }

    private bool CheckPortalShouldBeDeactivated(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        if (_mindSystem.GetAliveHumans().Any(c => Transform(c).MapID == portalFrame.Comp.DungeonMap))
            return false;
        return true;
    }

    private void ChangeShardHolesAppearance(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        if (portalFrame.Comp.ShardSlot1.Item is { } shard1)
        {
            var shardComp = EnsureComp<DungeonShardComponent>(shard1);
            _appearanceSystem.SetData(portalFrame, DungeonPortalVisuals.LeftState, shardComp.ColorInt);
        }
        if (portalFrame.Comp.ShardSlot1.Item is { } shard2)
        {
            var shardComp = EnsureComp<DungeonShardComponent>(shard2);
            _appearanceSystem.SetData(portalFrame, DungeonPortalVisuals.RightState, shardComp.ColorInt);
        }
    }

    private void ChangePortalAppearance(Entity<DungeonPortalFrameComponent> portalFrame)
    {
        int value = !portalFrame.Comp.IsActive ? 0
           : !portalFrame.Comp.IsOpened ? 1
           : 2;
        _appearanceSystem.SetData(portalFrame, DungeonPortalVisuals.OpenState, value);
    }
}
