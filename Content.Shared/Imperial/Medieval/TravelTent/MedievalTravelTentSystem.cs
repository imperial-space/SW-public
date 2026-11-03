using Content.Shared.Buckle.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.TravelTent;

/// <summary>
/// Pitching and folding the travel tent, and laying a sleeping bag out inside it.
/// </summary>
public sealed class MedievalTravelTentSystem : EntitySystem
{
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly FoldableSystem _foldable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> SleepingBagTag = "MedievalSleepingBag";

    // walls, tables, railings and other tents
    private const int BlockingMask = (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalTravelTentFoldedComponent, AfterInteractEvent>(OnFoldedAfterInteract);
        SubscribeLocalEvent<MedievalTravelTentFoldedComponent, MedievalTravelTentPitchDoAfterEvent>(OnPitchDoAfter);

        SubscribeLocalEvent<MedievalTravelTentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MedievalTravelTentComponent, ItemToggledEvent>(OnFlapToggled);
        SubscribeLocalEvent<MedievalTravelTentComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedievalTravelTentComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MedievalTravelTentComponent, MedievalTravelTentFoldDoAfterEvent>(OnFoldDoAfter);
    }

    private void OnFoldedAfterInteract(Entity<MedievalTravelTentFoldedComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // the floor, or a sleeping bag already laid out there
        EntityCoordinates location;
        if (args.Target is { } target)
        {
            if (!IsLaidOutSleepingBag(target))
                return;

            location = Transform(target).Coordinates;
        }
        else
        {
            location = args.ClickLocation;
        }

        args.Handled = true;
        location = location.SnapToGrid(EntityManager);

        if (!CanPitch(location))
        {
            _popup.PopupClient(Loc.GetString("medieval-travel-tent-no-room"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.PitchTime,
            new MedievalTravelTentPitchDoAfterEvent(GetNetCoordinates(location)),
            ent,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPitchDoAfter(Entity<MedievalTravelTentFoldedComponent> ent, ref MedievalTravelTentPitchDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var location = GetCoordinates(args.Location);
        if (!CanPitch(location))
        {
            _popup.PopupEntity(Loc.GetString("medieval-travel-tent-no-room"), ent, args.User);
            return;
        }

        var pitched = SpawnAtPosition(ent.Comp.Pitched, location);
        if (!TryComp<MedievalTravelTentComponent>(pitched, out var tentComp))
        {
            Log.Error($"{ToPrettyString(pitched)} has no {nameof(MedievalTravelTentComponent)}");
            QueueDel(pitched);
            return;
        }

        var tent = new Entity<MedievalTravelTentComponent>(pitched, tentComp);

        // pitched over a bag already lying there: put it in the middle
        var laidOut = FindLaidOutBag(tent);
        if (laidOut != null)
            _transform.SetCoordinates(laidOut.Value, BagSpot(tent));

        // the bag kept in the roll is laid out inside, or just left there if one already is
        if (_itemSlots.TryEject(ent, ent.Comp.Slot, null, out var bag))
        {
            if (laidOut == null)
                LayOut(tent, bag.Value);
            else
                _transform.SetCoordinates(bag.Value, BagSpot(tent));
        }

        QueueDel(ent);
    }

    private void OnMapInit(Entity<MedievalTravelTentComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<ItemToggleComponent>(ent, out var toggle))
            SetEntranceClosed(ent, toggle.Activated);
    }

    private void OnFlapToggled(Entity<MedievalTravelTentComponent> ent, ref ItemToggledEvent args)
    {
        SetEntranceClosed(ent, args.Activated);
    }

    private void SetEntranceClosed(Entity<MedievalTravelTentComponent> ent, bool closed)
    {
        if (_fixture.GetFixtureOrNull(ent, ent.Comp.EntranceFixture) is { } entrance)
            _physics.SetHard(ent, entrance, closed);
    }

    // a rolled sleeping bag used on the tent is laid out inside
    private void OnInteractUsing(Entity<MedievalTravelTentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, SleepingBagTag))
            return;

        if (!TryComp<FoldableComponent>(args.Used, out var foldable) || !foldable.IsFolded)
            return;

        args.Handled = true;

        if (FindLaidOutBag(ent) != null)
        {
            _popup.PopupClient(Loc.GetString("medieval-travel-tent-bag-already"), ent, args.User);
            return;
        }

        if (_hands.TryDrop(args.User, args.Used, BagSpot(ent)))
            LayOut(ent, args.Used);
    }

    private void OnGetVerbs(Entity<MedievalTravelTentComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("medieval-travel-tent-fold"),
            Act = () => StartFolding(ent, user),
        });
    }

    private void StartFolding(Entity<MedievalTravelTentComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime, new MedievalTravelTentFoldDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnFoldDoAfter(Entity<MedievalTravelTentComponent> ent, ref MedievalTravelTentFoldDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        // two people folding it in the same tick would get two tents
        if (_net.IsClient || TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        var coords = Transform(ent).Coordinates;
        var folded = SpawnAtPosition(ent.Comp.Folded, coords);

        // the bag inside is rolled up with the tent, unless someone is lying on it
        if (FindLaidOutBag(ent) is { } bag
            && TryComp<FoldableComponent>(bag, out var bagFoldable)
            && (!TryComp<StrapComponent>(bag, out var strap) || strap.BuckledEntities.Count == 0)
            && TryComp<MedievalTravelTentFoldedComponent>(folded, out var foldedComp))
        {
            _foldable.SetFolded(bag, bagFoldable, true);
            if (!_itemSlots.TryInsert(folded, foldedComp.Slot, bag, args.User))
                _foldable.SetFolded(bag, bagFoldable, false);
        }

        QueueDel(ent);
        _hands.PickupOrDrop(args.User, folded);
    }

    // the tent's walls are in the way of the bag's own unfold check, so lay it out directly
    private void LayOut(Entity<MedievalTravelTentComponent> tent, EntityUid bag)
    {
        if (!TryComp<FoldableComponent>(bag, out var foldable))
            return;

        _transform.SetCoordinates(bag, BagSpot(tent));
        _foldable.SetFolded(bag, foldable, false);
    }

    private bool IsLaidOutSleepingBag(EntityUid uid)
    {
        return _tag.HasTag(uid, SleepingBagTag)
            && TryComp<FoldableComponent>(uid, out var foldable)
            && !foldable.IsFolded;
    }

    private EntityCoordinates BagSpot(Entity<MedievalTravelTentComponent> tent)
    {
        return Transform(tent).Coordinates.Offset(tent.Comp.BagOffset);
    }

    private EntityUid? FindLaidOutBag(Entity<MedievalTravelTentComponent> tent)
    {
        var coords = Transform(tent).Coordinates;
        foreach (var uid in _lookup.GetEntitiesInRange(coords, tent.Comp.BagSearchRange, LookupFlags.Uncontained))
        {
            if (IsLaidOutSleepingBag(uid))
                return uid;
        }

        return null;
    }

    /// <summary>
    /// The tent stands on this tile and covers the one above: both have to exist and be free.
    /// </summary>
    private bool CanPitch(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var indices = _map.TileIndicesFor(gridUid, grid, coords);
        if (!TileFree((gridUid, grid), indices) || !TileFree((gridUid, grid), indices + Vector2i.Up))
            return false;

        // a tent on the tile below already covers this one
        var below = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices + Vector2i.Down);
        while (below.MoveNext(out var uid))
        {
            if (HasComp<MedievalTravelTentComponent>(uid))
                return false;
        }

        return true;
    }

    private bool TileFree(Entity<MapGridComponent> grid, Vector2i indices)
    {
        if (!_map.TryGetTileRef(grid, grid.Comp, indices, out var tile) || tile.Tile.IsEmpty)
            return false;

        if (!_anchorable.TileFree(grid, indices, collisionMask: BlockingMask))
            return false;

        // somebody standing there would end up inside the walls
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid.Owner, indices, -0.05f, LookupFlags.Dynamic))
        {
            if (HasComp<MobStateComponent>(uid))
                return false;
        }

        return true;
    }
}
