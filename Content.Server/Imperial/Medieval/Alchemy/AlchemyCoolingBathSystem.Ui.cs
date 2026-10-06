using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemyCoolingBathSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<AlchemyCoolingBathComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AlchemyCoolingBathComponent, EntInsertedIntoContainerMessage>(OnContentsInserted);
        SubscribeLocalEvent<AlchemyCoolingBathComponent, EntRemovedFromContainerMessage>(OnContentsRemoved);
        Subs.BuiEvents<AlchemyCoolingBathComponent>(AlchemyCoolingBathUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<AlchemyCoolingBathSelectTimeMessage>(OnSelectTime);
            subs.Event<AlchemyCoolingBathStartMessage>(OnStart);
            subs.Event<AlchemyCoolingBathEjectMessage>(OnEject);
        });
    }

    private void OnStartup(EntityUid uid, AlchemyCoolingBathComponent comp, ComponentStartup args)
    {
        _appearance.SetData(uid, AlchemyCoolingBathVisuals.IsProcessing, comp.IsProcessing);
        if (comp.SelectedDuration <= 0 || !comp.Durations.Contains(comp.SelectedDuration))
            comp.SelectedDuration = comp.Durations.FirstOrDefault(duration => duration > 0);
        UpdateUi(uid, comp);
    }

    private void OnContentsInserted(EntityUid uid, AlchemyCoolingBathComponent comp, EntInsertedIntoContainerMessage args)
    {
        UpdateUi(uid, comp);
    }

    private void OnContentsRemoved(EntityUid uid, AlchemyCoolingBathComponent comp, EntRemovedFromContainerMessage args)
    {
        UpdateUi(uid, comp);
    }

    private void OnUiOpened(EntityUid uid, AlchemyCoolingBathComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnSelectTime(EntityUid uid, AlchemyCoolingBathComponent comp, AlchemyCoolingBathSelectTimeMessage args)
    {
        if (comp.IsProcessing || args.Duration <= 0 || !comp.Durations.Contains(args.Duration))
            return;
        comp.SelectedDuration = args.Duration;
        UpdateUi(uid, comp);
    }

    private void OnStart(EntityUid uid, AlchemyCoolingBathComponent comp, AlchemyCoolingBathStartMessage args)
    {
        Start(uid, comp, comp.SelectedDuration);
    }

    private void OnEject(EntityUid uid, AlchemyCoolingBathComponent comp, AlchemyCoolingBathEjectMessage args)
    {
        if (comp.IsProcessing || !TryComp<AlchemyStationComponent>(uid, out var station))
            return;

        if (args.Item is { } netItem)
        {
            var item = GetEntity(netItem);
            if (!TerminatingOrDeleted(item) && station.Container.Contains(item))
                _hands.PickupOrDrop(args.Actor, item);
            return;
        }

        foreach (var item in station.Container.ContainedEntities.ToArray())
            _containers.Remove(item, station.Container);
    }

    private void UpdateUi(EntityUid uid, AlchemyCoolingBathComponent comp)
    {
        if (TerminatingOrDeleted(uid))
            return;
        var contents = TryComp<AlchemyStationComponent>(uid, out var station)
            ? station.Container.ContainedEntities.Where(item => !TerminatingOrDeleted(item)).ToArray()
            : Array.Empty<EntityUid>();
        _ui.SetUiState(uid, AlchemyCoolingBathUiKey.Key, new AlchemyCoolingBathUiState(
            GetNetEntityArray(contents), comp.Durations.Where(duration => duration > 0).Distinct().ToArray(),
            comp.SelectedDuration, comp.IsProcessing));
    }
}
