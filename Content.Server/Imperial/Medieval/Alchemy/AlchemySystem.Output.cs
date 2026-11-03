using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Imperial.Medieval.Alchemy;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    private void StoreApparatusProduct(EntityUid uid, EntityUid product)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
            !TryComp<AlchemyApparatusComponent>(uid, out var comp))
            return;
        if (!comp.OutputToInput)
        {
            if (_alchemyContainers.TryGetContainer(uid, comp.OutputContainer, out var output))
                _alchemyContainers.Insert(product, output);
            return;
        }
        if (!TryComp<AlchemyStationComponent>(uid, out var station))
            return;
        var completing = comp.Completing;
        comp.Completing = true;
        try
        {
            foreach (var item in station.Container.ContainedEntities.ToArray())
            {
                if (!TerminatingOrDeleted(item) && EntityManager.IsQueuedForDeletion(item))
                    _alchemyContainers.Remove(item, station.Container, force: true);
            }
            _alchemyContainers.Insert(product, station.Container);
        }
        finally
        {
            comp.Completing = completing;
        }
    }

    private void DistributeApparatusSolution(EntityUid uid, AlchemyApparatusComponent comp, Entity<SolutionComponent> input)
    {
        var recipients = comp.OutputToInput && TryComp<AlchemyStationComponent>(uid, out var station)
            ? station.Container.ContainedEntities.ToArray()
            : _alchemyContainers.TryGetContainer(uid, comp.OutputContainer, out var output)
                ? output.ContainedEntities.ToArray()
                : Array.Empty<EntityUid>();
        var inputVessel = Comp<AlchemyVesselComponent>(uid);
        inputVessel.Processing = true;
        try
        {
            var result = _solutions.SplitSolution(input, input.Comp.Solution.Volume);
            foreach (var recipient in recipients)
            {
                if (TerminatingOrDeleted(recipient) || EntityManager.IsQueuedForDeletion(recipient) ||
                    !_solutions.TryGetRefillableSolution(recipient, out var target, out var mixture) || target == null || mixture == null)
                    continue;
                if (mixture.AvailableVolume <= 0 || result.Volume <= 0)
                    continue;
                var vessel = EnsureComp<AlchemyVesselComponent>(recipient);
                vessel.Solution = mixture.Name ?? vessel.Solution;
                vessel.Processing = true;
                try
                {
                    _solutions.TryTransferSolution(target.Value, result, result.Volume);
                }
                finally
                {
                    UpdateTemperatureState(vessel, mixture);
                    vessel.Processing = false;
                }
            }
            if (result.Volume > 0)
                _puddles.TrySpillAt(Transform(uid).Coordinates, result, out _);
        }
        finally
        {
            UpdateTemperatureState(inputVessel, input.Comp.Solution);
            inputVessel.Processing = false;
        }
    }
}
