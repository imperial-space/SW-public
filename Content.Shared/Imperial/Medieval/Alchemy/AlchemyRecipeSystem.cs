using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Medieval.Alchemy;

public sealed class AlchemyRecipeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolutionComponent, SolutionChangedEvent>(OnSolutionChanged,
            before: new[] { typeof(SharedSolutionContainerSystem) });
        SubscribeLocalEvent<SolutionComponent, SolutionOverflowEvent>(OnSolutionOverflow,
            before: new[] { typeof(SharedSolutionContainerSystem) });
    }

    private void OnSolutionChanged(Entity<SolutionComponent> entity, ref SolutionChangedEvent args)
    {
        if (MergeHistories(entity.Comp.Solution))
            Dirty(entity);
    }

    private void OnSolutionOverflow(Entity<SolutionComponent> entity, ref SolutionOverflowEvent args)
    {
        if (MergeHistories(entity.Comp.Solution))
            Dirty(entity);
    }

    public static bool MergeHistories(Solution solution)
    {
        List<string>? history = null;
        var initialized = false;
        var mismatch = false;
        foreach (var entry in solution.Contents)
        {
            var current = entry.Reagent.Data?.OfType<AlchemyReagentData>().FirstOrDefault()?.Operations;
            if (!initialized)
            {
                history = current;
                initialized = true;
                continue;
            }

            if ((history ?? Enumerable.Empty<string>()).SequenceEqual(current ?? Enumerable.Empty<string>()))
                continue;

            mismatch = true;
            break;
        }

        if (!mismatch)
            return false;

        var contents = new Dictionary<ReagentId, FixedPoint2>();
        foreach (var entry in solution.Contents)
        {
            var data = entry.Reagent.Data?.Where(d => d is not AlchemyReagentData)
                .Select(d => d.Clone()).ToList();
            var reagent = new ReagentId(entry.Reagent.Prototype, data);
            contents.TryGetValue(reagent, out var quantity);
            contents[reagent] = quantity + entry.Quantity;
        }

        solution.SetContents(contents.Select(entry => new ReagentQuantity(entry.Key, entry.Value)).ToList());
        return true;
    }

    public static void RecordOperation(Solution solution, string operation, int historyLimit)
    {
        var contents = new Dictionary<ReagentId, FixedPoint2>();
        foreach (var entry in solution.Contents)
        {
            var data = entry.Reagent.Data?.Where(d => d is not AlchemyReagentData)
                .Select(d => d.Clone()).ToList() ?? new List<ReagentData>();
            var history = entry.Reagent.Data?.OfType<AlchemyReagentData>().FirstOrDefault();
            var next = new AlchemyReagentData();
            if (history != null)
                next.Operations.AddRange(history.Operations);
            next.Operations.Add(operation);
            if (next.Operations.Count > historyLimit)
                next.Operations.RemoveRange(0, next.Operations.Count - historyLimit);
            data.Add(next);
            var reagent = new ReagentId(entry.Reagent.Prototype, data);
            contents.TryGetValue(reagent, out var quantity);
            contents[reagent] = quantity + entry.Quantity;
        }

        solution.SetContents(contents.Select(entry => new ReagentQuantity(entry.Key, entry.Value)).ToList());
    }

    public static bool HasSuffix(ReagentId reagent, IReadOnlyList<string> steps)
    {
        var history = reagent.Data?.OfType<AlchemyReagentData>().FirstOrDefault()?.Operations;
        if (steps.Count == 0)
            return true;
        if (history == null || history.Count < steps.Count)
            return false;
        for (var i = 1; i <= steps.Count; i++)
        {
            if (history[^i] != steps[steps.Count - i])
                return false;
        }
        return true;
    }

    public static bool TryMatch(Solution solution, AlchemyRecipe recipe,
        out Dictionary<string, FixedPoint2> consumed, out Dictionary<string, FixedPoint2> products,
        out Dictionary<string, int> consumedEntities, out Dictionary<string, int> entityProducts,
        IReadOnlyDictionary<string, int>? entities = null)
    {
        consumedEntities = new();
        entityProducts = new();
        consumed = new();
        products = new();
        if (recipe.Ingredients.Count == 0 || (recipe.Products.Count == 0 && recipe.EntityProducts.Count == 0))
            return false;
        var totals = new Dictionary<string, long>();
        foreach (var entry in solution.Contents)
        {
            if (!recipe.Ingredients.ContainsKey(entry.Reagent.Prototype))
            {
                if (!recipe.AllowImpurities)
                    return false;
                continue;
            }
            if (!HasSuffix(entry.Reagent, recipe.Steps))
                return false;
            totals.TryGetValue(entry.Reagent.Prototype, out var total);
            totals[entry.Reagent.Prototype] = total + entry.Quantity.Value;
        }
        long numerator = 0;
        long denominator = 1;
        foreach (var (reagent, amount) in recipe.Ingredients)
        {
            if (amount <= 0 || !totals.TryGetValue(reagent, out var total) || total <= 0)
                return false;
            if (numerator == 0)
            {
                numerator = total;
                denominator = amount.Value;
            }
            else if (recipe.StrictRatio && total * denominator != numerator * amount.Value)
                return false;
            else if (total * denominator < numerator * amount.Value)
            {
                numerator = total;
                denominator = amount.Value;
            }
        }
        if (entities != null && !recipe.AllowImpurities && entities.Keys.Any(id => !recipe.Entities.ContainsKey(id)))
            return false;
        foreach (var (id, count) in recipe.Entities)
        {
            if (entities == null || !entities.TryGetValue(id, out var available) || available < count)
                return false;
            if (recipe.StrictRatio && (long) available * denominator != numerator * count)
                return false;
            if ((long) available * denominator < numerator * count)
            {
                numerator = available;
                denominator = count;
            }
        }
        if (recipe.Entities.Count > 0 || recipe.EntityProducts.Count > 0)
        {
            numerator /= denominator;
            denominator = 1;
            if (numerator <= 0 || numerator > int.MaxValue)
                return false;
            foreach (var (id, count) in recipe.Entities)
            {
                var value = numerator * count;
                if (value <= 0 || value > int.MaxValue)
                    return false;
                consumedEntities.Add(id, (int) value);
            }
            foreach (var (id, count) in recipe.EntityProducts)
            {
                var value = numerator * count;
                if (value <= 0 || value > int.MaxValue)
                    return false;
                entityProducts.Add(id, (int) value);
            }
        }
        foreach (var (reagent, amount) in recipe.Ingredients)
        {
            var value = amount.Value * numerator / denominator;
            if (value <= 0 || value > int.MaxValue)
                return false;
            consumed.Add(reagent, FixedPoint2.FromCents((int) value));
        }
        foreach (var (reagent, amount) in recipe.Products)
        {
            var value = amount.Value * numerator / denominator;
            if (value <= 0 || value > int.MaxValue)
                return false;
            products.Add(reagent, FixedPoint2.FromCents((int) value));
        }
        return true;
    }
}
