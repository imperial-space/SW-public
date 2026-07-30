using System;
using System.Linq;
using System.Numerics;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.ShipBuyTerminal;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.ShipBuyTerminal;

public sealed class ShipBuyTerminalSystem : EntitySystem
{
    private const float ContactTolerance = 0.001f;
    private static readonly TimeSpan PurchaseLockDuration = TimeSpan.FromSeconds(2);

    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipBuyTerminalComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<ShipBuyTerminalComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ShipBuyTerminalComponent, ShipBuyTerminalBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<ShipBuyTerminalComponent, ShipBuyTerminalWithdrawMessage>(OnRequestWithdraw);
        SubscribeLocalEvent<ShipBuyTerminalComponent, InteractUsingEvent>(OnCurrencyInsert);
    }

    private void OnOpenAttempt(EntityUid uid, ShipBuyTerminalComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_mobState.IsAlive(args.User))
        {
            args.Cancel();
            return;
        }

        UpdateUi(uid, component);
    }

    private void OnBeforeUiOpen(EntityUid uid, ShipBuyTerminalComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnCurrencyInsert(EntityUid uid, ShipBuyTerminalComponent component, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CurrencyComponent>(args.Used, out var currency))
            return;

        var currencyId = (string)component.Currency;
        if (!currency.Price.TryGetValue(currencyId, out var pricePerUnit))
            return;

        var amount = TryComp<StackComponent>(args.Used, out var stack) ? stack.Count : 1;
        component.Balance += (int)(pricePerUnit * amount);

        args.Handled = true;
        QueueDel(args.Used);
        UpdateUi(uid, component);
    }

    private List<ShipGridOfferPrototype> GetAllOffers()
    {
        return [.. _prototype.EnumeratePrototypes<ShipGridOfferPrototype>().OrderBy(p => p.ID)];
    }

    private void OnBuyRequest(EntityUid uid, ShipBuyTerminalComponent component, ShipBuyTerminalBuyMessage msg)
    {
        var user = msg.Actor;
        if (!_mobState.IsAlive(user))
            return;

        if (IsPurchaseLocked(component))
        {
            UpdateUi(uid, component);
            return;
        }

        var offers = GetAllOffers();
        if (msg.OfferIndex < 0 || msg.OfferIndex >= offers.Count)
            return;

        LockPurchases(uid, component);

        var offer = offers[msg.OfferIndex];

        if (component.Balance < offer.Cost)
            return;

        var mapId = _transform.GetMapId(uid);
        var worldPos = _transform.GetWorldPosition(uid);

        var offsetAngle = Angle.FromDegrees(component.GlobalOffsetAngle + offer.LocalOffsetAngle);
        var gridAngle = Angle.FromDegrees(component.GlobalGridAngle + offer.GridAngle);
        var totalOffset = component.GlobalOffset + offer.LocalOffset;
        var spawnPos = worldPos + offsetAngle.ToVec() * totalOffset;

        var path = new ResPath(offer.GridPath);
        var options = DeserializationOptions.Default with
        {
            InitializeMaps = true,
            PauseMaps = true,
        };

        if (!_mapLoader.TryLoadGrid(path, out var stagingMap, out var grid, options, rot: gridAngle))
            return;

        var stagingMapId = stagingMap.Value.Comp.MapId;
        try
        {
            if (IsSpawnBlocked(grid.Value, mapId, spawnPos, gridAngle))
            {
                _popup.PopupEntity(
                    Loc.GetString("ship-buy-terminal-docking-blocked"),
                    uid,
                    user,
                    PopupType.MediumCaution);
                return;
            }

            var targetMap = _map.GetMap(mapId);
            _transform.SetCoordinates(
                grid.Value.Owner,
                Transform(grid.Value.Owner),
                new EntityCoordinates(targetMap, spawnPos),
                rotation: gridAngle);

            component.Balance -= offer.Cost;
            UpdateUi(uid, component);
        }
        finally
        {
            _map.DeleteMap(stagingMapId);
        }
    }

    private bool IsSpawnBlocked(
        Entity<MapGridComponent> ship,
        MapId targetMapId,
        Vector2 spawnPosition,
        Angle spawnRotation)
    {
        var shipTiles = new List<Box2Rotated>();
        var sourceTiles = _map.GetAllTilesEnumerator(ship.Owner, ship.Comp);
        while (sourceTiles.MoveNext(out var sourceTile))
        {
            var localBounds = GetLocalBounds(sourceTile.Value, ship.Comp.TileSize);
            var worldBounds = ToWorldBounds(localBounds, spawnPosition, spawnRotation);
            shipTiles.Add(worldBounds);
        }

        var prospectiveBounds = ToWorldBounds(ship.Comp.LocalAABB, spawnPosition, spawnRotation);
        var queryBounds = prospectiveBounds.Enlarged(ContactTolerance);
        var targetGrids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(targetMapId, queryBounds, ref targetGrids);

        var targetGridData = new List<(Entity<MapGridComponent> Grid, Vector2 Position, Angle Rotation)>();
        foreach (var targetGrid in targetGrids)
        {
            var (targetPosition, targetRotation) = _transform.GetWorldPositionRotation(targetGrid.Owner);
            targetGridData.Add((targetGrid, targetPosition, targetRotation));
        }

        foreach (var shipTile in shipTiles)
        {
            var tileQueryBounds = shipTile.Enlarged(ContactTolerance);
            foreach (var targetGrid in targetGridData)
            {
                var targetTiles = _map.GetTilesEnumerator(
                    targetGrid.Grid.Owner,
                    targetGrid.Grid.Comp,
                    tileQueryBounds);

                while (targetTiles.MoveNext(out var targetTile))
                {
                    var localBounds = GetLocalBounds(targetTile, targetGrid.Grid.Comp.TileSize);
                    var worldBounds = ToWorldBounds(localBounds, targetGrid.Position, targetGrid.Rotation);

                    if (Intersects(shipTile, worldBounds))
                        return true;
                }
            }
        }

        return false;
    }

    private static Box2 GetLocalBounds(TileRef tile, ushort tileSize)
    {
        return new Box2(tile.GridIndices * tileSize, (tile.GridIndices + Vector2i.One) * tileSize);
    }

    private static Box2Rotated ToWorldBounds(Box2 localBounds, Vector2 worldPosition, Angle worldRotation)
    {
        return new Box2Rotated(localBounds.Translated(worldPosition), worldRotation, worldPosition);
    }

    private static bool Intersects(Box2Rotated first, Box2Rotated second)
    {
        var firstX = first.Rotation.RotateVec(Vector2.UnitX);
        var firstY = first.Rotation.RotateVec(Vector2.UnitY);
        var secondX = second.Rotation.RotateVec(Vector2.UnitX);
        var secondY = second.Rotation.RotateVec(Vector2.UnitY);

        return OverlapsOnAxis(first, second, firstX) &&
               OverlapsOnAxis(first, second, firstY) &&
               OverlapsOnAxis(first, second, secondX) &&
               OverlapsOnAxis(first, second, secondY);
    }

    private static bool OverlapsOnAxis(Box2Rotated first, Box2Rotated second, Vector2 axis)
    {
        var distance = MathF.Abs(Vector2.Dot(second.Center - first.Center, axis));
        return distance <= GetProjectionRadius(first, axis) +
               GetProjectionRadius(second, axis) +
               ContactTolerance;
    }

    private static float GetProjectionRadius(Box2Rotated bounds, Vector2 axis)
    {
        var halfSize = bounds.Box.Size / 2f;
        var horizontal = bounds.Rotation.RotateVec(Vector2.UnitX);
        var vertical = bounds.Rotation.RotateVec(Vector2.UnitY);

        return halfSize.X * MathF.Abs(Vector2.Dot(horizontal, axis)) +
               halfSize.Y * MathF.Abs(Vector2.Dot(vertical, axis));
    }

    private bool IsPurchaseLocked(ShipBuyTerminalComponent component)
    {
        return component.PurchaseLockedUntil > _timing.CurTime;
    }

    private void LockPurchases(EntityUid uid, ShipBuyTerminalComponent component)
    {
        component.PurchaseLockedUntil = _timing.CurTime + PurchaseLockDuration;
        UpdateUi(uid, component);

        Timer.Spawn(PurchaseLockDuration, () =>
        {
            if (!TryComp<ShipBuyTerminalComponent>(uid, out var currentComponent) ||
                IsPurchaseLocked(currentComponent))
            {
                return;
            }

            UpdateUi(uid, currentComponent);
        });
    }

    private void OnRequestWithdraw(EntityUid uid, ShipBuyTerminalComponent component, ShipBuyTerminalWithdrawMessage msg)
    {
        if (msg.Amount <= 0)
            return;

        var user = msg.Actor;
        if (!_mobState.IsAlive(user))
            return;

        if (component.Balance < msg.Amount)
            return;

        if (!_prototype.TryIndex(component.Currency, out var proto))
            return;

        if (proto.Cash == null || !proto.CanWithdraw)
            return;

        FixedPoint2 amountRemaining = msg.Amount;
        var coordinates = Transform(user).Coordinates;

        var sortedCashValues = proto.Cash.Keys.OrderByDescending(x => x).ToList();
        foreach (var value in sortedCashValues)
        {
            var cashId = proto.Cash[value];
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            if (amountToSpawn <= 0)
                continue;

            var ents = _stack.SpawnMultiple(cashId, amountToSpawn, coordinates);
            if (ents.FirstOrDefault() is { } ent)
                _hands.PickupOrDrop(user, ent);

            amountRemaining -= value * amountToSpawn;
        }

        component.Balance -= msg.Amount;
        UpdateUi(uid, component);
    }

    private void UpdateUi(EntityUid uid, ShipBuyTerminalComponent component)
    {
        var offerIds = GetAllOffers().Select(p => p.ID).ToList();
        var state = new ShipBuyTerminalUpdateState(
            component.Balance,
            offerIds,
            component.Currency,
            IsPurchaseLocked(component));
        _ui.SetUiState(uid, ShipBuyTerminalUiKey.Key, state);
    }
}
