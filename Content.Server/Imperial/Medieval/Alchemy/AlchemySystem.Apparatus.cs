using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;
using Content.Shared.Interaction;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    private void InitializeApparatus()
    {
        InitializeApparatusUi();
        SubscribeLocalEvent<AlchemyApparatusComponent, ActivateInWorldEvent>(OnApparatusActivate);
        SubscribeLocalEvent<AlchemyApparatusComponent, ContainerIsInsertingAttemptEvent>(OnApparatusInsert);
        SubscribeLocalEvent<AlchemyApparatusComponent, ContainerIsRemovingAttemptEvent>(OnApparatusRemove);
        SubscribeLocalEvent<AlchemyApparatusComponent, ComponentShutdown>(OnApparatusShutdown);
        SubscribeLocalEvent<AlchemyApparatusComponent, EntRemovedFromContainerMessage>(OnApparatusContentsRemoved);
        SubscribeLocalEvent<AlchemyApparatusComponent, SolutionContainerChangedEvent>(OnApparatusSolutionChanged);
    }

    private void OnApparatusShutdown(EntityUid uid, AlchemyApparatusComponent comp, ComponentShutdown args)
    {
        StopApparatus(uid, comp);
    }

    private void OnApparatusContentsRemoved(EntityUid uid, AlchemyApparatusComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.IsProcessing && !comp.Completing &&
            (args.Entity == comp.Input || comp.Items.Contains(args.Entity)))
            StopApparatus(uid, comp);
        UpdateApparatusUi(uid, comp);
    }

    private void OnApparatusSolutionChanged(EntityUid uid, AlchemyApparatusComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (!comp.IsProcessing || args.SolutionId != comp.Solution)
            return;

        var previousVolume = comp.InputVolume;
        comp.InputVolume = args.Solution.Volume;
        if (args.Solution.Volume < previousVolume &&
            (!TryComp<AlchemyVesselComponent>(uid, out var vessel) || !vessel.Processing))
            StopApparatus(uid, comp);
    }

    private async Task RunApparatus(EntityUid uid, AlchemyApparatusComponent comp, uint generation, float duration)
    {
        try
        {
            await Robust.Shared.Timing.Timer.Delay(TimeSpan.FromSeconds(duration));

            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !TryComp<AlchemyApparatusComponent>(uid, out var current) || current != comp ||
                !comp.IsProcessing || comp.ProcessingGeneration != generation)
                return;

            if (!_solutions.TryGetSolution(uid, comp.Solution, out var input, out _) || input == null ||
                input.Value.Owner != comp.Input || TerminatingOrDeleted(input.Value.Owner) ||
                EntityManager.IsQueuedForDeletion(input.Value.Owner) ||
                !HasComp<AlchemyVesselComponent>(uid) || !TryComp<AlchemyStationComponent>(uid, out var station) ||
                comp.Items.Any(item => TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item) ||
                    !station.Container.Contains(item)))
                return;

            FinishApparatus(uid, comp, input.Value);
        }
        catch (Exception exception)
        {
            Log.Error($"Alchemy apparatus {uid} failed: {exception}");
        }
        finally
        {
            if (comp.ProcessingGeneration == generation)
                StopApparatus(uid, comp);
        }
    }

    private void OnApparatusInsert(EntityUid uid, AlchemyApparatusComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == comp.OutputContainer)
        {
            if (comp.OutputToInput || args.Container.ContainedEntities.Count >= comp.OutputCapacity)
                args.Cancel();
            return;
        }
        if (comp.IsProcessing && !comp.Completing)
            args.Cancel();
    }

    private void OnApparatusRemove(EntityUid uid, AlchemyApparatusComponent comp, ContainerIsRemovingAttemptEvent args)
    {
        if (comp.IsProcessing && !comp.Completing)
            args.Cancel();
    }

    private void OnApparatusActivate(EntityUid uid, AlchemyApparatusComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        UpdateApparatusUi(uid, comp);
        _alchemyUi.TryOpenUi(uid, AlchemyUiKey.Key, args.User);
    }

    private void StartApparatus(EntityUid uid, AlchemyApparatusComponent comp, EntityUid user)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
            comp.IsProcessing || !TryComp<AlchemyStationComponent>(uid, out var station))
            return;
        var operation = _prototypes.Index<AlchemyOperationPrototype>(comp.Operation);
        if (operation.Temperature != null || !TryGetRoundState(out _))
            return;
        if (!_solutions.TryGetSolution(uid, comp.Solution, out var input, out var mixture) || input == null || mixture == null)
            return;
        if (station.Container.ContainedEntities.Count == 0 && mixture.Volume <= 0)
        {
            _popup.PopupEntity(Loc.GetString(operation.EmptyMessage), uid, user);
            return;
        }

        var vessel = EnsureComp<AlchemyVesselComponent>(uid);
        vessel.Solution = comp.Solution;
        vessel.Processing = true;
        comp.Items.Clear();
        try
        {
            foreach (var item in station.Container.ContainedEntities.ToArray())
            {
                if (_solutions.TryGetDrainableSolution(item, out var source, out var liquid) && source != null && liquid != null)
                    mixture.AddSolution(_solutions.SplitSolution(source.Value, liquid.Volume), _prototypes);
                else
                {
                    comp.Items.Add(item);
                    EnsureComp<AlchemyItemHistoryComponent>(item);
                }
            }
            UpdateTemperatureState(vessel, mixture);
            _solutions.UpdateChemicals(input.Value, false);
        }
        finally
        {
            vessel.Processing = false;
        }

        comp.User = user;
        comp.Input = input.Value.Owner;
        comp.InputVolume = mixture.Volume;
        comp.IsProcessing = true;
        var generation = ++comp.ProcessingGeneration;
        UpdateApparatusUi(uid, comp);
        _popup.PopupEntity(Loc.GetString(operation.RunningMessage), uid, user);
        _ = RunApparatus(uid, comp, generation, operation.Duration);
    }

    private void FinishApparatus(EntityUid uid, AlchemyApparatusComponent comp, Entity<SolutionComponent> input)
    {
        var user = comp.User is { } actor && !TerminatingOrDeleted(actor) ? comp.User : null;
        try
        {
            if (!TryGetRoundState(out var state))
                return;

            comp.Completing = true;
            var items = comp.Items.Where(item => !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item)).ToList();
            var operation = _prototypes.Index<AlchemyOperationPrototype>(comp.Operation);
            var inputVessel = Comp<AlchemyVesselComponent>(uid);
            inputVessel.Processing = true;
            try
            {
                CompleteOperation(input, inputVessel, state, comp.Operation, user, items, uid);
            }
            finally
            {
                inputVessel.Processing = false;
            }

            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                TerminatingOrDeleted(input.Owner) || EntityManager.IsQueuedForDeletion(input.Owner))
                return;

            DistributeApparatusSolution(uid, comp, input);
            if (user is { } recipient)
                _popup.PopupEntity(Loc.GetString(operation.CompletionMessage), uid, recipient);
        }
        finally
        {
            StopApparatus(uid, comp);
        }
    }

    private void StopApparatus(EntityUid uid, AlchemyApparatusComponent comp)
    {
        comp.IsProcessing = false;
        comp.ProcessingGeneration++;
        comp.Completing = false;
        comp.Items.Clear();
        comp.User = null;
        comp.Input = null;
        comp.InputVolume = default;
        UpdateApparatusUi(uid, comp);
    }
}
