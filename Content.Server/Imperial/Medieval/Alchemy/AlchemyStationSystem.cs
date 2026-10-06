using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed class AlchemyStationSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _items = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AlchemyStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AlchemyStationComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AlchemyStationComponent, ContainerIsInsertingAttemptEvent>(OnInsert);
        SubscribeLocalEvent<AlchemyStationComponent, DestructionEventArgs>(OnDestroy);
    }

    private void OnInit(EntityUid uid, AlchemyStationComponent comp, ComponentInit args)
    {
        comp.Container = _containers.EnsureContainer<Container>(uid, comp.ContainerId);
    }

    private void OnInsert(EntityUid uid, AlchemyStationComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;
        if (!TryComp<ItemComponent>(args.EntityUid, out var item) ||
            _items.GetSizePrototype(item.Size) > _items.GetSizePrototype(comp.MaxItemSize))
        {
            args.Cancel();
            return;
        }

        var used = _items.GetItemShape(item).GetArea();
        if (!args.AssumeEmpty)
        {
            foreach (var stored in comp.Container.ContainedEntities)
            {
                if (TryComp<ItemComponent>(stored, out var storedItem))
                    used += _items.GetItemShape(storedItem).GetArea();
            }
        }
        if (used > comp.Capacity)
            args.Cancel();
    }

    private void OnInteractUsing(EntityUid uid, AlchemyStationComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !_containers.CanInsert(args.Used, comp.Container))
            return;

        args.Handled = true;
        _hands.TryDropIntoContainer(args.User, args.Used, comp.Container);
    }

    private void OnDestroy(EntityUid uid, AlchemyStationComponent comp, DestructionEventArgs args)
    {
        var coordinates = _transform.GetMoverCoordinates(uid);
        _containers.EmptyContainer(comp.Container, force: true, destination: coordinates);
        if (TryComp<AlchemyApparatusComponent>(uid, out var apparatus) && !apparatus.OutputToInput &&
            _containers.TryGetContainer(uid, apparatus.OutputContainer, out var output))
            _containers.EmptyContainer(output, force: true, destination: coordinates);
    }
}
