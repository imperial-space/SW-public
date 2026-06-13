using System.Linq;
using Content.Server.Stack;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Ships.ShipBuyTerminal;
using Content.Shared.Mobs.Systems;
using Content.Shared.Store;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.ShipBuyTerminal;

public sealed class ShipBuyTerminalSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipBuyTerminalComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<ShipBuyTerminalComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ShipBuyTerminalComponent, ShipBuyTerminalBuyMessage>(OnBuyRequest);
        SubscribeLocalEvent<ShipBuyTerminalComponent, ShipBuyTerminalWithdrawMessage>(OnRequestWithdraw);
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

    private void OnBuyRequest(EntityUid uid, ShipBuyTerminalComponent component, ShipBuyTerminalBuyMessage msg)
    {
        var user = msg.Actor;
        if (!_mobState.IsAlive(user))
            return;

        if (msg.OfferIndex < 0 || msg.OfferIndex >= component.GridOffers.Count)
            return;

        var offerId = component.GridOffers[msg.OfferIndex];
        if (!_prototype.TryIndex(offerId, out var offer))
            return;

        if (component.Balance < offer.Cost)
            return;

        var mapId = _transform.GetMapId(uid);
        var worldPos = _transform.GetWorldPosition(uid);

        var spawnAngle = new Angle(component.SpawnAngle).Reduced();
        var totalOffset = component.GlobalOffset + offer.LocalOffset;
        var spawnPos = worldPos + spawnAngle.ToVec() * totalOffset;

        var path = new ResPath(offer.GridPath);
        if (!_mapLoader.TryLoadGrid(mapId, path, out var grid, offset: spawnPos))
            return;

        if (grid != null)
            _transform.SetWorldRotation(grid.Value, spawnAngle);

        component.Balance -= offer.Cost;
        UpdateUi(uid, component);
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

            _stack.SpawnMultiple(cashId, amountToSpawn, coordinates);
            amountRemaining -= value * amountToSpawn;
        }

        component.Balance -= msg.Amount;
        UpdateUi(uid, component);
    }

    private void UpdateUi(EntityUid uid, ShipBuyTerminalComponent component)
    {
        var offerIds = component.GridOffers.Select(id => (string) id).ToList();
        var state = new ShipBuyTerminalUpdateState(component.Balance, offerIds, component.Currency);
        _ui.SetUiState(uid, ShipBuyTerminalUiKey.Key, state);
    }
}
