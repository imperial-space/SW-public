using System;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Ships.Oar;

public sealed class OarSystem : EntitySystem
{
    private const float OarUseRange = 2f;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, AfterInteractEvent>(OnOarAfterInteract);
        SubscribeAllEvent<OarUseRequestEvent>(OnOarUseRequest);
    }

    private void OnOarAfterInteract(EntityUid uid, OarComponent component, AfterInteractEvent args)
    {
        if (!_net.IsClient || !_timing.IsFirstTimePredicted || args.Handled || !args.CanReach)
            return;

        if (!TryGetGrid(args.User, out var boat))
            return;

        if (!HasComp<MapComponent>(args.ClickLocation.EntityId))
            return;

        var clickMapCoordinates = _transform.ToMapCoordinates(args.ClickLocation);
        var clickGridCoordinates = _transform.ToCoordinates(boat, clickMapCoordinates);

        RaisePredictiveEvent(new OarUseRequestEvent(GetNetEntity(uid), GetNetCoordinates(clickGridCoordinates)));
    }

    private void OnOarUseRequest(OarUseRequestEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } playerEntity)
            return;

        var oar = GetEntity(args.Oar);
        var clickCoordinates = GetCoordinates(args.ClickCoordinates);

        if (!clickCoordinates.IsValid(EntityManager) ||
            !TryComp<OarComponent>(oar, out var component) ||
            _hands.GetActiveItem(playerEntity) != oar ||
            !TryComp<WieldableComponent>(oar, out var wieldable) ||
            !wieldable.Wielded ||
            !_skills.HasSkill(playerEntity, SharedSkillsSystem.StrengthId) ||
            !TryGetGrid(playerEntity, out var boat) ||
            clickCoordinates.EntityId != boat ||
            !_interaction.InRangeUnobstructed(playerEntity, clickCoordinates, OarUseRange) ||
            !TryGetGridDirection(_transform.GetMoverCoordinates(playerEntity), clickCoordinates, out var gridDirection))
        {
            return;
        }

        if (TryComp<MapGridComponent>(boat, out var mapGrid) &&
            _map.TryGetTileRef(boat, mapGrid, clickCoordinates, out var tile) &&
            !tile.Tile.IsEmpty)
        {
            return;
        }

        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.3f;
        time = Math.Max(1.0f, time);
        var doAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new OnOarDoAfterEvent(),
            eventTarget: oar,
            target: null,
            used: oar)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = null,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        component.GridDirection = gridDirection;
        _popup.PopupClient($"Ты гребёшь от себя", playerEntity);
    }

    private bool TryGetGrid(EntityUid uid, out EntityUid grid)
    {
        grid = _transform.GetMoverCoordinates(uid).EntityId;
        return HasComp<MapGridComponent>(grid);
    }

    public static bool TryGetGridDirection(
        EntityCoordinates playerCoordinates,
        EntityCoordinates clickCoordinates,
        out Vector2 direction)
    {
        direction = Vector2.Zero;

        if (playerCoordinates.EntityId != clickCoordinates.EntityId)
            return false;

        var offset = playerCoordinates.Position - clickCoordinates.Position;
        var lengthSquared = offset.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.0001f)
            return false;

        direction = offset / MathF.Sqrt(lengthSquared);
        return true;
    }

    public static Vector2 GetWorldDirection(Vector2 gridDirection, Angle gridRotation)
    {
        return gridRotation.RotateVec(gridDirection);
    }
}
