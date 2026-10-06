using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.Alchemy;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private void InitializeRound()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        if (TryGetRoundState(out _))
            return;

        var generation = new AlchemyGenerationComponent();
        var state = GenerateRoundState(generation);
        var uid = Spawn(null, new MapCoordinates(Vector2.Zero, _gameTicker.DefaultMap));
        AddComp(uid, generation);
        AddComp(uid, state);
    }

    private bool TryGetRoundState([NotNullWhen(true)] out AlchemyRoundComponent? state)
    {
        var query = EntityManager.AllEntityQueryEnumerator<AlchemyRoundComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;

            state = component;
            return true;
        }

        state = null;
        return false;
    }

    private AlchemyRoundComponent GenerateRoundState(AlchemyGenerationComponent generation)
    {
        var state = new AlchemyRoundComponent();
        var random = new System.Random(_random.Next());
        var operations = GetOperations();
        GenerateRecipes(state, operations, random, generation);
        GenerateIngredients(state, random, generation);
        return state;
    }

    private Dictionary<string, AlchemyOperationPrototype> GetOperations()
    {
        var operations = _prototypes.EnumeratePrototypes<AlchemyOperationPrototype>().ToDictionary(operation => operation.ID);
        foreach (var operation in operations.Values)
        {
            if (operation.Complexity <= 0 || operation.Duration < 0 || !float.IsFinite(operation.Duration) ||
                operation.Temperature is { } temperature && (!float.IsFinite(temperature) || temperature < 0))
                throw new InvalidOperationException($"Invalid alchemy operation {operation.ID}.");
        }
        return operations;
    }

    private void GenerateRecipes(AlchemyRoundComponent state,
        IReadOnlyDictionary<string, AlchemyOperationPrototype> operations, System.Random random,
        AlchemyGenerationComponent generation)
    {
        var prototypes = _prototypes.EnumeratePrototypes<AlchemyRecipePrototype>()
            .Where(prototype => !prototype.Abstract)
            .OrderBy(prototype => prototype.Randomized)
            .ThenBy(prototype => prototype.ID, StringComparer.Ordinal);
        foreach (var prototype in prototypes)
        {
            var recipe = GenerateUniqueRecipe(prototype, state.Recipes, operations, random, generation);
            foreach (var reagent in recipe.Ingredients.Keys.Concat(recipe.Products.Keys))
                _prototypes.Index<ReagentPrototype>(reagent);
            foreach (var entity in recipe.Entities.Keys.Concat(recipe.EntityProducts.Keys))
                _prototypes.Index<EntityPrototype>(entity);
            state.Recipes.Add(recipe);
            state.HistoryLimit = Math.Max(state.HistoryLimit, recipe.Steps.Count);
        }
        state.Recipes.Sort((left, right) => left.Priority != right.Priority
            ? right.Priority.CompareTo(left.Priority)
            : string.CompareOrdinal(left.Id, right.Id));
    }

    private static AlchemyRecipe GenerateUniqueRecipe(AlchemyRecipePrototype prototype,
        IReadOnlyList<AlchemyRecipe> recipes, IReadOnlyDictionary<string, AlchemyOperationPrototype> operations,
        System.Random random, AlchemyGenerationComponent generation)
    {
        AlchemyRecipe? conflict = null;
        var attempts = prototype.Randomized ? 512 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var candidate = AlchemyGenerationSystem.GenerateRecipe(prototype, operations, random, generation);
            conflict = recipes.FirstOrDefault(recipe => AlchemyGenerationSystem.Conflicts(recipe, candidate));
            if (conflict == null)
                return candidate;
        }
        throw new InvalidOperationException($"Alchemy recipe {prototype.ID} conflicts with {conflict?.Id} after {attempts} generation attempts.");
    }

    private void GenerateIngredients(AlchemyRoundComponent state, System.Random random,
        AlchemyGenerationComponent generation)
    {
        var profiles = _prototypes.EnumeratePrototypes<AlchemyIngredientPrototype>()
            .OrderBy(profile => profile.ID, StringComparer.Ordinal).ToArray();
        var requiredAspects = state.Recipes.SelectMany(recipe => recipe.Ingredients.Keys)
            .Where(reagent => AlchemyGenerationSystem.IsAspect(reagent, generation)).ToHashSet();
        for (var attempt = 0; attempt < 512; attempt++)
        {
            state.Ingredients.Clear();
            foreach (var profile in profiles)
            {
                _prototypes.Index<ReagentPrototype>(profile.ID);
                var ingredient = AlchemyGenerationSystem.GenerateIngredient(profile, random);
                _prototypes.Index<ReagentPrototype>(ingredient.Solvent);
                foreach (var aspect in ingredient.Aspects.Keys)
                    _prototypes.Index<ReagentPrototype>(aspect);
                state.Ingredients.Add(profile.ID, ingredient);
            }
            if (requiredAspects.IsSubsetOf(state.Ingredients.Values.SelectMany(ingredient => ingredient.Aspects.Keys)))
                return;
        }
        throw new InvalidOperationException("Alchemy ingredients cannot supply the generated recipes.");
    }
}
