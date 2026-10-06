using System.Linq;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Medieval.Alchemy;

public sealed class AlchemyGenerationSystem : EntitySystem
{
    public static string Pick(System.Random random, Dictionary<string, int> weights)
    {
        var total = weights.Values.Where(w => w > 0).Sum();
        if (total <= 0)
            throw new InvalidOperationException("Alchemy requires a positive choice weight.");
        var choice = random.Next(total);
        foreach (var (id, weight) in weights.OrderBy(p => p.Key, Comparer<string>.Create(string.CompareOrdinal)))
        {
            if (weight <= 0)
                continue;
            choice -= weight;
            if (choice < 0)
                return id;
        }
        throw new InvalidOperationException("Invalid alchemy weights.");
    }

    public static AlchemyIngredient GenerateIngredient(AlchemyIngredientPrototype proto, System.Random random)
    {
        if (proto.ReagentAmount <= 0 || proto.MinYield <= 0 || proto.MaxYield < proto.MinYield || proto.AspectCount <= 0 ||
            proto.Solvents.Count == 0 || proto.AspectWeights.Count(p => p.Value > 0) < proto.AspectCount ||
            proto.MinYield < proto.AspectCount)
            throw new InvalidOperationException($"Invalid alchemy ingredient {proto.ID}.");
        var result = new AlchemyIngredient
        {
            ReagentAmount = proto.ReagentAmount,
            Solvent = proto.Solvents[random.Next(proto.Solvents.Count)],
        };
        var weights = new Dictionary<string, int>(proto.AspectWeights);
        var remaining = random.Next(proto.MinYield, proto.MaxYield + 1);
        for (var i = 0; i < proto.AspectCount; i++)
        {
            var aspect = Pick(random, weights);
            weights.Remove(aspect);
            var amount = i == proto.AspectCount - 1 ? remaining : random.Next(1, remaining - (proto.AspectCount - i - 1) + 1);
            result.Aspects.Add(aspect, FixedPoint2.New(amount));
            remaining -= amount;
        }
        return result;
    }

    public static AlchemyRecipe GenerateRecipe(AlchemyRecipePrototype proto,
        IReadOnlyDictionary<string, AlchemyOperationPrototype> operations, System.Random random,
        AlchemyGenerationComponent generation)
    {
        if (proto.Abstract || proto.MaxComplexity < 0 || proto.ExpectedSteps < 0 || proto.ExpectedSteps > 64 ||
            proto.MinParts <= 0 || proto.MaxParts < proto.MinParts || (proto.Products.Count == 0 && proto.EntityProducts.Count == 0) ||
            proto.Products.Any(p => p.Value <= 0) || proto.EntityProducts.Any(p => p.Value <= 0) ||
            proto.Entities.Any(p => p.Value <= 0))
            throw new InvalidOperationException($"Invalid alchemy recipe {proto.ID}.");
        if (!proto.Randomized && (proto.Ingredients == null || proto.Steps == null))
            throw new InvalidOperationException($"Fixed alchemy recipe {proto.ID} is incomplete.");
        var result = new AlchemyRecipe
        {
            Id = proto.ID,
            Group = proto.Group,
            Products = new(proto.Products),
            EntityProducts = new(proto.EntityProducts),
            Entities = new(proto.Entities),
            StrictRatio = proto.StrictRatio ?? (!proto.Randomized || random.Next(4) != 0),
            AllowImpurities = proto.AllowImpurities ?? (proto.Randomized && random.Next(4) == 0),
            Priority = proto.Priority,
        };
        var ingredients = proto.Ingredients ?? Enumerable.Range(0, proto.IngredientCount)
            .Select(_ => new AlchemyIngredientRequirement()).ToList();
        var weights = new Dictionary<string, int>(proto.AspectWeights);
        foreach (var (aspect, minimumTier) in generation.AspectMinimumTiers)
        {
            if (proto.Tier < minimumTier)
                weights.Remove(aspect);
        }
        foreach (var ingredient in ingredients)
        {
            if (ingredient.Reagent != null)
                weights.Remove(ingredient.Reagent);
        }
        var selected = new List<(string Reagent, FixedPoint2? Amount)>();
        foreach (var ingredient in ingredients)
        {
            if (!proto.Randomized && (ingredient.Reagent == null || ingredient.Amount == null))
                throw new InvalidOperationException($"Fixed alchemy recipe {proto.ID} has an incomplete ingredient.");
            var reagent = ingredient.Reagent ?? Pick(random, weights);
            weights.Remove(reagent);
            selected.Add((reagent, ingredient.Amount));
        }
        var remainingAspects = proto.TotalAspectAmount;
        var missingAspects = 0;
        if (remainingAspects is { } total)
        {
            if (total <= 0)
                throw new InvalidOperationException($"Invalid aspect total in alchemy recipe {proto.ID}.");
            foreach (var (reagent, amount) in selected)
            {
                if (!IsAspect(reagent, generation))
                    continue;
                if (amount is { } fixedAmount)
                    remainingAspects -= fixedAmount;
                else
                    missingAspects++;
            }
            if (remainingAspects < FixedPoint2.New(proto.MinParts) * missingAspects ||
                remainingAspects > FixedPoint2.New(proto.MaxParts) * missingAspects)
                throw new InvalidOperationException($"Alchemy recipe {proto.ID} cannot satisfy its aspect total without changing manual amounts.");
        }
        foreach (var (reagent, specifiedAmount) in selected)
        {
            var amount = specifiedAmount ?? FixedPoint2.Zero;
            if (specifiedAmount == null)
            {
                if (remainingAspects is { } remaining && IsAspect(reagent, generation))
                {
                    missingAspects--;
                    var minimum = FixedPoint2.Max(FixedPoint2.New(proto.MinParts), remaining - FixedPoint2.New(proto.MaxParts) * missingAspects);
                    var maximum = FixedPoint2.Min(FixedPoint2.New(proto.MaxParts), remaining - FixedPoint2.New(proto.MinParts) * missingAspects);
                    var minimumWhole = (minimum.Value + 99) / 100;
                    var maximumWhole = maximum.Value / 100;
                    amount = missingAspects == 0 ? remaining : minimumWhole <= maximumWhole
                        ? FixedPoint2.New(random.Next(minimumWhole, maximumWhole + 1))
                        : minimum;
                    remainingAspects -= amount;
                }
                else
                    amount = FixedPoint2.New(random.Next(proto.MinParts, proto.MaxParts + 1));
            }
            if (amount <= 0 || !result.Ingredients.TryAdd(reagent, amount) ||
                (generation.AspectMinimumTiers.TryGetValue(reagent, out var minimumTier) && proto.Tier < minimumTier))
                throw new InvalidOperationException($"Invalid ingredient in alchemy recipe {proto.ID}.");
        }
        if (result.Ingredients.Count == 0)
            throw new InvalidOperationException($"Alchemy recipe {proto.ID} has no ingredients.");
        var steps = proto.Steps ?? Enumerable.Range(0, proto.ExpectedSteps).Select(_ => new AlchemyStep()).ToList();
        if (steps.Count > 64)
            throw new InvalidOperationException($"Too many steps in alchemy recipe {proto.ID}.");
        var budget = proto.MaxComplexity;
        foreach (var step in steps)
        {
            if (step.Operation == null)
                continue;
            if (!operations.TryGetValue(step.Operation, out var operation))
                throw new InvalidOperationException($"Invalid operation in alchemy recipe {proto.ID}.");
            budget -= operation.Complexity;
        }
        if (budget < 0)
            throw new InvalidOperationException($"Alchemy recipe {proto.ID} exceeds its complexity budget.");
        var candidates = operations.Values.Where(o => o.MinimumTier <= proto.Tier && o.Weight > 0 && o.Complexity > 0).ToList();
        var missing = steps.Count(s => s.Operation == null);
        if (missing > 0 && (!proto.Randomized || candidates.Count == 0))
            throw new InvalidOperationException($"Cannot generate steps for alchemy recipe {proto.ID}.");
        var cheapest = candidates.Count == 0 ? 0 : candidates.Min(o => o.Complexity);
        if (missing * cheapest > budget)
            throw new InvalidOperationException($"Alchemy recipe {proto.ID} cannot fit its expected steps in its budget.");
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var previous = result.Steps.Count > 0 ? operations[result.Steps[^1]] : null;
            var next = index + 1 < steps.Count && steps[index + 1].Operation is { } nextId
                ? operations[nextId]
                : null;
            if (step.Operation != null)
            {
                if (RepeatsTemperatureOperation(previous, operations[step.Operation]))
                    throw new InvalidOperationException($"Alchemy recipe {proto.ID} repeats a temperature operation.");
                result.Steps.Add(step.Operation);
                continue;
            }
            missing--;
            var eligible = candidates.Where(o => o.Complexity <= budget - missing * cheapest &&
                    !RepeatsTemperatureOperation(previous, o) && !RepeatsTemperatureOperation(o, next))
                .ToDictionary(o => o.ID, o => o.Weight);
            if (eligible.Count == 0)
                throw new InvalidOperationException($"Alchemy recipe {proto.ID} has no valid next operation within its complexity budget.");
            var id = Pick(random, eligible);
            result.Steps.Add(id);
            budget -= operations[id].Complexity;
        }
        return result;
    }

    public static bool IsAspect(string reagent, AlchemyGenerationComponent generation) =>
        generation.AspectMinimumTiers.ContainsKey(reagent);

    private static bool RepeatsTemperatureOperation(AlchemyOperationPrototype? left, AlchemyOperationPrototype? right)
    {
        return left?.Temperature != null && right?.Temperature != null && left.Heating == right.Heating;
    }

    public static bool Conflicts(AlchemyRecipe left, AlchemyRecipe right)
    {
        if (!CanShareContents(left, right) || !CanShareContents(right, left))
            return false;
        if (left.StrictRatio && right.StrictRatio && !HaveCompatibleRatios(left, right))
            return false;
        return ContainsSteps(left.Steps, right.Steps) || ContainsSteps(right.Steps, left.Steps);
    }

    private static bool CanShareContents(AlchemyRecipe recipe, AlchemyRecipe other)
    {
        return recipe.AllowImpurities ||
               other.Ingredients.Keys.All(recipe.Ingredients.ContainsKey) &&
               other.Entities.Keys.All(recipe.Entities.ContainsKey);
    }

    private static bool HaveCompatibleRatios(AlchemyRecipe left, AlchemyRecipe right)
    {
        long leftScale = 0;
        long rightScale = 0;
        foreach (var (leftAmount, rightAmount) in SharedAmounts(left, right))
        {
            if (leftScale == 0)
            {
                leftScale = leftAmount;
                rightScale = rightAmount;
                continue;
            }
            if (leftAmount * rightScale != rightAmount * leftScale)
                return false;
        }
        return true;
    }

    private static IEnumerable<(long Left, long Right)> SharedAmounts(AlchemyRecipe left, AlchemyRecipe right)
    {
        foreach (var (reagent, amount) in left.Ingredients)
        {
            if (right.Ingredients.TryGetValue(reagent, out var otherAmount))
                yield return (amount.Value, otherAmount.Value);
        }
        foreach (var (entity, count) in left.Entities)
        {
            if (right.Entities.TryGetValue(entity, out var otherCount))
                yield return (count, otherCount);
        }
    }

    private static bool ContainsSteps(IReadOnlyList<string> sequence, IReadOnlyList<string> steps)
    {
        for (var offset = 0; offset <= sequence.Count - steps.Count; offset++)
        {
            var matches = true;
            for (var index = 0; index < steps.Count; index++)
            {
                if (sequence[offset + index] == steps[index])
                    continue;
                matches = false;
                break;
            }
            if (matches)
                return true;
        }
        return false;
    }
}
