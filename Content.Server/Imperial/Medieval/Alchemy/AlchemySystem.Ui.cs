using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _alchemyUi = default!;
    [Dependency] private readonly SharedContainerSystem _alchemyContainers = default!;
    [Dependency] private readonly SharedHandsSystem _alchemyHands = default!;

    private void InitializeApparatusUi()
    {
        SubscribeLocalEvent<AlchemyApparatusComponent, ComponentStartup>(OnApparatusStartup);
        SubscribeLocalEvent<AlchemyApparatusComponent, EntInsertedIntoContainerMessage>(OnApparatusContentsInserted);
        Subs.BuiEvents<AlchemyApparatusComponent>(AlchemyUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnApparatusUiOpened);
            subs.Event<AlchemyInsertMessage>(OnApparatusUiInsert);
            subs.Event<AlchemyTakeMessage>(OnApparatusUiTake);
            subs.Event<AlchemyStartMessage>(OnApparatusUiStart);
        });
    }

    private void OnApparatusStartup(EntityUid uid, AlchemyApparatusComponent comp, ComponentStartup args)
    {
        if (!comp.OutputToInput)
            _alchemyContainers.EnsureContainer<Container>(uid, comp.OutputContainer);
        UpdateApparatusUi(uid, comp);
    }

    private void OnApparatusContentsInserted(EntityUid uid, AlchemyApparatusComponent comp, EntInsertedIntoContainerMessage args)
    {
        UpdateApparatusUi(uid, comp);
    }

    private void OnApparatusUiOpened(EntityUid uid, AlchemyApparatusComponent comp, BoundUIOpenedEvent args)
    {
        UpdateApparatusUi(uid, comp);
    }

    private void OnApparatusUiStart(EntityUid uid, AlchemyApparatusComponent comp, AlchemyStartMessage args)
    {
        StartApparatus(uid, comp, args.Actor);
    }

    private void OnApparatusUiInsert(EntityUid uid, AlchemyApparatusComponent comp, AlchemyInsertMessage args)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
            !_alchemyHands.TryGetActiveItem(args.Actor, out var item))
            return;

        if (args.Output)
        {
            if (comp.OutputToInput || !_alchemyContainers.TryGetContainer(uid, comp.OutputContainer, out var output) ||
                !_alchemyContainers.CanInsert(item.Value, output) || !_alchemyHands.TryDrop(args.Actor, item.Value))
                return;
            _alchemyContainers.Insert(item.Value, output);
        }
        else
        {
            if (comp.IsProcessing || !TryComp<AlchemyStationComponent>(uid, out var station) ||
                !_alchemyContainers.CanInsert(item.Value, station.Container) ||
                !_alchemyHands.TryDrop(args.Actor, item.Value))
                return;
            _alchemyContainers.Insert(item.Value, station.Container);
        }
    }

    private void OnApparatusUiTake(EntityUid uid, AlchemyApparatusComponent comp, AlchemyTakeMessage args)
    {
        if (comp.IsProcessing || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;
        var item = GetEntity(args.Item);
        if (TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
            return;
        var inInput = TryComp<AlchemyStationComponent>(uid, out var station) && station.Container.Contains(item);
        var inOutput = !comp.OutputToInput &&
            _alchemyContainers.TryGetContainer(uid, comp.OutputContainer, out var output) && output.Contains(item);
        if (inInput || inOutput)
            _alchemyHands.PickupOrDrop(args.Actor, item);
    }

    private void UpdateApparatusUi(EntityUid uid, AlchemyApparatusComponent comp)
    {
        if (TerminatingOrDeleted(uid))
            return;
        var input = TryComp<AlchemyStationComponent>(uid, out var station)
            ? station.Container.ContainedEntities.Where(item => !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item)).ToArray()
            : Array.Empty<EntityUid>();
        var output = !comp.OutputToInput && _alchemyContainers.TryGetContainer(uid, comp.OutputContainer, out var container)
            ? container.ContainedEntities.Where(item => !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item)).ToArray()
            : Array.Empty<EntityUid>();
        _alchemyUi.SetUiState(uid, AlchemyUiKey.Key,
            new AlchemyUiState(GetNetEntityArray(input), GetNetEntityArray(output), comp.IsProcessing, comp.OutputToInput, comp.OutputCapacity));
    }

}
