using Content.Shared._RD.Weight.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Imperial.Medieval.Ships.WaterPump.Bucket;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This handles...
/// </summary>
public sealed class WaterPumpBucketSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem  _solution = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpBucketComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WaterPumpBucketComponent, BucketUseEvent>(OnBucketUse);
    }

    private void OnAfterInteract(EntityUid uid, WaterPumpBucketComponent component, AfterInteractEvent args)
    {
        var playerEntity = args.User;

        if (args.Handled || !args.CanReach )
            return;

        var boat = _transform.GetParentUid(playerEntity);

        var clickEntity = args.ClickLocation.EntityId;
        if (boat != clickEntity)
            return;
        // if (_solution.PercentFull(args.Used) >= 100)
        //     return;

        if (HasComp<MapGridComponent>(boat) || HasComp<ShipDrowningComponent>(boat))
            return;

        _popup.PopupClient($"Ты вычёрпываешь воду с корабля", playerEntity);
        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.15f - _skills.GetSkillLevel(playerEntity, "Strength") * 0.15f;
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
    }

    private void OnBucketUse(EntityUid uid, WaterPumpBucketComponent component, BucketUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
            return;
        //TryComp<SolutionContainerManagerComponent>(uid, out var bucket);

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
    }
}
