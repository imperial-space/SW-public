using System.Linq;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Stack;
using Content.Server.MedievalPotionChecker.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Alchemy;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly PuddleSystem _puddles = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        InitializeApparatus();
        InitializeRound();
        InitializeChemMaster();
        SubscribeLocalEvent<AlchemyIngredientComponent, ExaminedEvent>(OnExamineIngredient);
        SubscribeLocalEvent<ReactionMixerComponent, ComponentStartup>(OnReactionMixerStartup);
        SubscribeLocalEvent<AlchemyMixerComponent, ReactionMixDoAfterEvent>(OnReactionMixFinished,
            before: new[] { typeof(ReactionMixerSystem) });
        SubscribeLocalEvent<AlchemyVesselComponent, SolutionContainerChangedEvent>(OnVesselChanged);
        SubscribeLocalEvent<MixableSolutionComponent, SolutionContainerChangedEvent>(OnMixableChanged);
        SubscribeLocalEvent<AlchemyVesselComponent, SolutionContainerOverflowEvent>(OnOverflow);
    }

    private void OnExamineIngredient(EntityUid uid, AlchemyIngredientComponent comp, ExaminedEvent args)
    {
        if (!HasComp<MedievalPotionCheckerComponent>(args.Examiner) ||
            !TryGetRoundState(out var state) || !state.Ingredients.TryGetValue(comp.Profile, out var ingredient))
            return;
        var aspects = string.Join(", ", ingredient.Aspects.Select(p => $"{ReagentName(p.Key)}: {p.Value}"));
        args.PushText(Loc.GetString("alchemy-ingredient-description", ("solvent", ReagentName(ingredient.Solvent)),
            ("aspects", aspects)));
    }

    public string ReagentName(string id)
    {
        var proto = _prototypes.Index<ReagentPrototype>(id);
        return string.IsNullOrWhiteSpace(proto.LocalizedName) ? id : proto.LocalizedName;
    }

    private void OnReactionMixerStartup(EntityUid uid, ReactionMixerComponent comp, ComponentStartup args)
    {
        EnsureComp<AlchemyMixerComponent>(uid);
    }

    private void OnReactionMixFinished(EntityUid uid, AlchemyMixerComponent comp, ref ReactionMixDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || TerminatingOrDeleted(target) ||
            !TryComp<ReactionMixerComponent>(uid, out var mixer) ||
            !mixer.ReactionTypes.Any(type => type.Id == "Stir"))
            return;

        var attempt = new MixingAttemptEvent(uid);
        RaiseLocalEvent(uid, ref attempt);
        if (attempt.Cancelled || !_solutions.TryGetMixableSolution(target, out var solution, out _) ||
            solution == null || solution.Value.Comp.Solution.Volume <= 0 || !TryGetRoundState(out var state))
            return;

        var vessel = EnsureComp<AlchemyVesselComponent>(target);
        vessel.Solution = solution.Value.Comp.Solution.Name ?? vessel.Solution;
        if (vessel.Processing)
            return;
        vessel.Processing = true;
        try
        {
            CompleteOperation(solution.Value, vessel, state, "Stir", args.User);
        }
        finally
        {
            UpdateTemperatureState(vessel, solution.Value.Comp.Solution);
            vessel.Processing = false;
        }
    }

    private void OnMixableChanged(EntityUid uid, MixableSolutionComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != comp.Solution || HasComp<AlchemyVesselComponent>(uid) || args.Solution.Volume <= 0)
            return;
        var currentTemperature = args.Solution.Temperature;
        if (!_prototypes.EnumeratePrototypes<AlchemyOperationPrototype>().Any(operation =>
                operation.Temperature is { } temperature &&
                (operation.Heating ? currentTemperature >= temperature : currentTemperature <= temperature)))
            return;
        var vessel = EnsureComp<AlchemyVesselComponent>(uid);
        vessel.Solution = comp.Solution;
        OnVesselChanged(uid, vessel, ref args);
    }

    private void UpdateTemperatureState(AlchemyVesselComponent vessel, Solution solution)
    {
        vessel.Hot = solution.Volume > 0 && solution.Temperature >= _prototypes.Index(vessel.HeatOperation).Temperature;
        vessel.Cold = solution.Volume > 0 && solution.Temperature <= _prototypes.Index(vessel.CoolOperation).Temperature;
    }

    private void OnVesselChanged(EntityUid uid, AlchemyVesselComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (comp.Processing || args.SolutionId != comp.Solution || !TryGetRoundState(out var state))
            return;
        var wasHot = comp.Hot;
        var wasCold = comp.Cold;
        UpdateTemperatureState(comp, args.Solution);
        if (args.Solution.Volume <= 0 || !_solutions.TryGetSolution(uid, comp.Solution, out var solution, out _) || solution == null)
            return;
        comp.Processing = true;
        try
        {
            Extract(comp, solution.Value, state);
            var apparatus = CompOrNull<AlchemyApparatusComponent>(uid);
            var items = apparatus is { IsProcessing: true } ? apparatus.Items : null;
            var user = apparatus?.User is { } actor && !TerminatingOrDeleted(actor) ? apparatus.User : null;
            if (comp.Hot && !wasHot)
                CompleteOperation(solution.Value, comp, state, comp.HeatOperation.Id, user, items, apparatus != null ? uid : null);
            else if (comp.Cold && !wasCold)
                CompleteOperation(solution.Value, comp, state, comp.CoolOperation.Id, user, items, apparatus != null ? uid : null);
        }
        finally
        {
            comp.Processing = false;
        }
    }

    private void OnOverflow(EntityUid uid, AlchemyVesselComponent comp, ref SolutionContainerOverflowEvent args)
    {
        if (args.Handled)
            return;
        _puddles.TrySpillAt(Transform(uid).Coordinates, args.Overflow, out _);
        args.Handled = true;
    }

    private void Extract(AlchemyVesselComponent vessel, Entity<SolutionComponent> solution,
        AlchemyRoundComponent state)
    {
        var mixture = solution.Comp.Solution;
        if (mixture.Temperature < vessel.NigredoTemperature)
            return;
        var changed = false;
        foreach (var reagent in mixture.Contents.Select(entry => entry.Reagent.Prototype).Distinct().ToArray())
        {
            if (!state.Ingredients.TryGetValue(reagent, out var profile))
                continue;
            var output = profile.Aspects.Values.Aggregate(FixedPoint2.Zero, (a, b) => a + b);
            var cost = output * 0.25;
            var available = mixture.GetTotalPrototypeQuantity(reagent);
            var solvent = mixture.GetTotalPrototypeQuantity(profile.Solvent);
            var portions = Math.Min(available.Value / profile.ReagentAmount.Value, solvent.Value / cost.Value);
            if (portions <= 0)
                continue;
            RemovePrototype(mixture, reagent, profile.ReagentAmount * portions);
            RemovePrototype(mixture, profile.Solvent, cost * portions);
            foreach (var (aspect, amount) in profile.Aspects)
                mixture.AddReagent(aspect, amount * portions);
            changed = true;
        }
        if (changed)
            _solutions.UpdateChemicals(solution, false);
    }

    private void CompleteOperation(Entity<SolutionComponent> solution, AlchemyVesselComponent vessel,
        AlchemyRoundComponent state, string operation,
        EntityUid? user = null,
        IReadOnlyList<EntityUid>? items = null, EntityUid? apparatus = null)
    {
        AlchemyRecipeSystem.RecordOperation(solution.Comp.Solution, operation, state.HistoryLimit);
        if (solution.Comp.Solution.Volume > 0)
            _audio.PlayPvs(vessel.StepSound, solution.Owner);
        if (items != null)
        {
            items = items.Where(item => !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item)).ToList();
            foreach (var item in items)
            {
                var history = EnsureComp<AlchemyItemHistoryComponent>(item).Operations;
                history.Add(operation);
                if (history.Count > state.HistoryLimit)
                    history.RemoveRange(0, history.Count - state.HistoryLimit);
            }
        }
        ExecuteRecipes(solution, state, user, items, apparatus);
    }

    private void ExecuteRecipes(Entity<SolutionComponent> solution, AlchemyRoundComponent state, EntityUid? user = null,
        IReadOnlyList<EntityUid>? items = null, EntityUid? apparatus = null)
    {
        foreach (var recipe in state.Recipes)
        {
            var entities = new Dictionary<string, int>();
            var matchingHistory = true;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (MetaData(item).EntityPrototype is not { } proto)
                        continue;
                    var history = Comp<AlchemyItemHistoryComponent>(item).Operations;
                    if (recipe.Entities.ContainsKey(proto.ID) && !history.TakeLast(recipe.Steps.Count).SequenceEqual(recipe.Steps))
                        matchingHistory = false;
                    entities.TryGetValue(proto.ID, out var count);
                    entities[proto.ID] = count + (TryComp<StackComponent>(item, out var stack) ? stack.Count : 1);
                }
            }
            if (!matchingHistory || !AlchemyRecipeSystem.TryMatch(solution.Comp.Solution, recipe, out var consumed, out var products,
                    out var consumedEntities, out var entityProducts, entities))
                continue;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (MetaData(item).EntityPrototype is not { } proto ||
                        !consumedEntities.TryGetValue(proto.ID, out var remaining) || remaining <= 0 ||
                        !Comp<AlchemyItemHistoryComponent>(item).Operations.TakeLast(recipe.Steps.Count).SequenceEqual(recipe.Steps))
                        continue;
                    var count = TryComp<StackComponent>(item, out var stack) ? Math.Min(stack.Count, remaining) : 1;
                    consumedEntities[proto.ID] -= count;
                    if (stack != null)
                        _stacks.SetCount(item, stack.Count - count);
                    else
                        QueueDel(item);
                }
            }
            foreach (var (reagent, amount) in consumed)
                RemovePrototype(solution.Comp.Solution, reagent, amount);
            foreach (var (reagent, amount) in products)
                solution.Comp.Solution.AddReagent(reagent, amount);
            foreach (var (prototype, count) in entityProducts)
            {
                var coordinates = _transform.GetMapCoordinates(solution.Owner);
                for (var i = 0; i < count; i++)
                {
                    var product = Spawn(prototype, coordinates);
                    _transform.AttachToGridOrMap(product);
                    if (apparatus is { } station)
                        StoreApparatusProduct(station, product);
                }
            }
            if (user is { } actor && TryComp<AffectRoundStatsComponent>(actor, out var stats))
                stats.Potions++;
            var counters = EntityQueryEnumerator<RoundStatCounterRuleComponent>();
            while (counters.MoveNext(out var counter))
                counter.TotalPotions++;
            foreach (var (reagent, amount) in products)
            {
                if (!_prototypes.TryIndex<ReactionPrototype>(reagent, out var reaction))
                    continue;
                var scale = amount / recipe.Products[reagent];
                var effectArgs = new EntityEffectReagentArgs(solution, EntityManager, null,
                    solution.Comp.Solution, scale, null, null, 1f);
                foreach (var effect in reaction.Effects)
                {
                    if (effect.ShouldApply(effectArgs))
                        effect.Effect(effectArgs);
                }
            }
            break;
        }
        _solutions.UpdateChemicals(solution, false);
    }

    private static void RemovePrototype(Solution solution, string prototype, FixedPoint2 amount)
    {
        foreach (var entry in solution.Contents.ToArray())
        {
            if (entry.Reagent.Prototype != prototype)
                continue;
            amount -= solution.RemoveReagent(entry.Reagent, FixedPoint2.Min(amount, entry.Quantity));
            if (amount <= 0)
                return;
        }
    }
}

