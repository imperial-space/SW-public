using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    private void InitializeChemMaster()
    {
        SubscribeLocalEvent<ChemMasterComponent, ComponentInit>(OnChemMasterInit);
        SubscribeLocalEvent<AlchemyChemMasterComponent, ChemMasterReagentAmountButtonMessage>(OnChemMasterTransfer,
            after: new[] { typeof(ChemMasterSystem) });
    }

    private void OnChemMasterInit(Entity<ChemMasterComponent> entity, ref ComponentInit args)
    {
        EnsureComp<AlchemyChemMasterComponent>(entity.Owner);
    }

    private void OnChemMasterTransfer(Entity<AlchemyChemMasterComponent> entity, ref ChemMasterReagentAmountButtonMessage args)
    {
        if (args.FromBuffer || !TryComp<ChemMasterComponent>(entity.Owner, out var chemMaster) ||
            chemMaster.Mode != ChemMasterMode.Transfer ||
            !_solutions.TryGetSolution(entity.Owner, SharedChemMaster.BufferSolutionName, out var buffer, out var solution))
            return;

        if (!AlchemyRecipeSystem.MergeHistories(solution))
            return;

        Dirty(buffer.Value);
        var changed = new SolutionChangedEvent(buffer.Value);
        RaiseLocalEvent(buffer.Value.Owner, ref changed);
    }
}
