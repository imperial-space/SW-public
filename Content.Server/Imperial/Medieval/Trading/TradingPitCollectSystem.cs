using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Store.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed class TradingPitCollectSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly TradingSystem _trading = default!;

    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<EntityUid> _entList = new();
    private readonly HashSet<EntityUid> _entSet = new();

    public override void Initialize()
    {
        base.Initialize();
        _itemQuery = GetEntityQuery<ItemComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<TradingPitCollectComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);
        SubscribeLocalEvent<TradingPitCollectComponent, TradingPitCollectDoAfterEvent>(OnDoAfter);
    }

    private void OnGetAltVerb(EntityUid uid, TradingPitCollectComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("trading-pit-collect-verb"),
            Act = () => StartCollect(uid, args.User, component)
        });
    }

    private void StartCollect(EntityUid pit, EntityUid user, TradingPitCollectComponent component)
    {
        if (!_xformQuery.TryGetComponent(pit, out var pitXform))
            return;

        if (!_interaction.InRangeUnobstructed(user, pit))
            return;

        _entList.Clear();
        _entSet.Clear();

        _lookup.GetEntitiesInRange(pitXform.Coordinates, component.Radius, _entSet, LookupFlags.Dynamic | LookupFlags.Sundries);
        var delay = 0f;

        foreach (var entity in _entSet)
        {
            if (entity == user)
                continue;

            if (!_itemQuery.TryGetComponent(entity, out var itemComp))
                continue;

            if (!_prototype.Resolve(itemComp.Size, out var itemSize))
                continue;

            if (!IsSellableCandidate(pit, entity))
                continue;

            if (!_interaction.InRangeUnobstructed(user, entity))
                continue;

            _entList.Add(entity);
            delay += itemSize.Weight * component.DelayPerWeight;

            if (_entList.Count >= component.PickupLimit)
                break;
        }

        if (_entList.Count == 0)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new TradingPitCollectDoAfterEvent(GetNetEntityList(_entList)),
            pit,
            target: pit)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private bool IsSellableCandidate(EntityUid pit, EntityUid item)
    {
        if (TryComp<TradingComponent>(pit, out var trading))
        {
            if (!TryComp<MedievalCurrencyComponent>(item, out var currency) || currency.Price.Count == 0)
                return false;

            foreach (var (type, _) in currency.Price)
            {
                if (trading.Currency != type)
                    return false;
            }

            return true;
        }

        if (TryComp<StoreComponent>(pit, out var store))
        {
            if (!TryComp<CurrencyComponent>(item, out var currency) || currency.Price.Count == 0)
                return false;

            foreach (var (type, _) in currency.Price)
            {
                if (!store.CurrencyWhitelist.Contains(type))
                    return false;
            }

            return true;
        }

        return false;
    }

    private void OnDoAfter(EntityUid uid, TradingPitCollectComponent component, TradingPitCollectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!_xformQuery.TryGetComponent(uid, out var pitXform))
            return;

        var sold = new List<EntityUid>();
        var soldPositions = new List<EntityCoordinates>();
        var soldAngles = new List<Angle>();

        var entCount = Math.Min(component.PickupLimit, args.Entities.Count);

        for (var i = 0; i < entCount; i++)
        {
            var entity = GetEntity(args.Entities[i]);

            if (entity == args.Args.User || !_itemQuery.HasComponent(entity))
                continue;

            if (_container.IsEntityInContainer(entity))
                continue;

            if (!_xformQuery.TryGetComponent(entity, out var targetXform) || targetXform.MapID != pitXform.MapID)
                continue;

            var position = _transform.ToCoordinates(
                pitXform.ParentUid.IsValid() ? pitXform.ParentUid : uid,
                new MapCoordinates(_transform.GetWorldPosition(targetXform), targetXform.MapID));

            var angle = targetXform.LocalRotation;

            if (!TrySellEntity(args.Args.User, uid, entity))
                continue;

            sold.Add(entity);
            soldPositions.Add(position);
            soldAngles.Add(angle);
        }

        if (sold.Count > 0)
        {
            EntityManager.RaiseSharedEvent(new AnimateInsertingEntitiesEvent(
                GetNetEntity(uid),
                GetNetEntityList(sold),
                GetNetCoordinatesList(soldPositions),
                soldAngles), args.Args.User);
        }
    }

    private bool TrySellEntity(EntityUid user, EntityUid pit, EntityUid entity)
    {
        if (TryComp<TradingComponent>(pit, out var trading) &&
            TryComp<MedievalCurrencyComponent>(entity, out var medievalCurrency))
        {
            return _trading.TryAddCurrency((entity, medievalCurrency), (pit, trading));
        }

        if (TryComp<StoreComponent>(pit, out var store) &&
            TryComp<CurrencyComponent>(entity, out var currency))
        {
            var ev = new CurrencyInsertAttemptEvent(user, pit, entity, store);
            RaiseLocalEvent(pit, ev);
            if (ev.Cancelled)
                return false;

            return _store.TryAddCurrency((entity, currency), (pit, store));
        }

        return false;
    }
}
