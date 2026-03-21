using Content.Shared._RD.Weight.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Imperial.Medieval.Ships.Repairing;

/// <summary>
/// This handles...
/// </summary>
public sealed class ShipRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RepairMaterialComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<RepairMaterialComponent, RepairUseEvent>(OnRepairUse);
    }

    private void OnAfterInteract(EntityUid uid, RepairMaterialComponent component, AfterInteractEvent args)
    {
        var playerEntity = args.User;

        if (args.Handled || !args.CanReach )
            return;

        var boat = _transform.GetParentUid(playerEntity);

        var clickEntity = args.ClickLocation.EntityId;
        if (boat != clickEntity)
            return;
        TryComp<MapGridComponent>(boat, out var boatComponent);
        if (boatComponent == null)
            return;
        _map.TryGetTileRef(boat, boatComponent, args.ClickLocation,  out var tile);
        _tileDefinitionManager.TryGetDefinition("FloorBrokenWoodDDD", out var floor);
        _tileDefinitionManager.TryGetDefinition(tile.Tile.TypeId, out var test);
        if (test == null)
            return;

        if (floor == null || tile.Tile.TypeId != floor.TileId)
            return;
        _popup.PopupClient($"Ты начинаешь закрывать дыры доской", playerEntity);
        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.05f - _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.25f;
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new RepairUseEvent(),
            args.Used,
            boat,
            args.Used)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,

        };
        _doAfter.TryStartDoAfter(sdoAfter);
        component.TileCord = tile.GridIndices;
    }

    private void OnRepairUse(EntityUid uid, RepairMaterialComponent component, RepairUseEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;
        TryComp<MapGridComponent>(args.Target, out var mapGrid);
        _tileDefinitionManager.TryGetDefinition("woodenfloor", out var floor);
        if (mapGrid == null || floor == null)
            return;
        _map.SetTile(args.Target.Value, mapGrid, component.TileCord, new Tile(floor.TileId));
        _stack.Use(uid, 1);
    }
}
