using Content.Shared._RD.Weight.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.WaterPump.Bucket;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This handles...
/// </summary>
public sealed class WaterPumpSystem : EntitySystem
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
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WaterPumpComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<WaterPumpComponent, PumpUseEvent>(OnBucketUse);
    }

    private void OnAfterInteract(EntityUid uid, WaterPumpComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach )
            return;
        Use(args.User, uid);
    }

    private void OnActivateInWorld(EntityUid uid, WaterPumpComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        Use(args.User,  uid);
    }

    private void Use(EntityUid playerEntity, EntityUid used)
    {
        var boat = _transform.GetParentUid(used);

        TryComp<MapGridComponent>(boat, out var boatComponent);
        if (boatComponent == null)
            return;

        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.05f - _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.25f;
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new PumpUseEvent(),
            used,
            boat,
            used)
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

    private void OnBucketUse(EntityUid uid, WaterPumpComponent component, PumpUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
            return;

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
        args.Repeat = true;
        args.Handled = true;
    }
}
