using System.Linq;
using System.Text;
using Content.Shared.Imperial.Medieval.Alchemy;
using Content.Shared.Imperial.Medieval.Chemistry;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed partial class AlchemySystem
{
    public AlchemyRecipe? ResolveScroll(MedievalRandomChemistryRecipeComponent scroll)
    {
        if (!TryGetRoundState(out var state))
            return null;
        var available = state.Recipes.Where(recipe => _prototypes.Index<AlchemyRecipePrototype>(recipe.Id).Randomized).ToList();
        if (scroll.RecipeId != null)
        {
            var existing = available.FirstOrDefault(recipe => recipe.Id == scroll.RecipeId);
            if (existing != null)
                return existing;
            scroll.RecipeId = null;
        }
        var groups = scroll.Weights.Where(p => p.Value > 0 && available.Any(r => r.Group == p.Key)).ToList();
        if (groups.Count == 0)
            return null;
        var choice = _random.NextFloat() * groups.Sum(p => p.Value);
        var group = groups[^1].Key;
        foreach (var entry in groups)
        {
            choice -= entry.Value;
            if (choice > 0)
                continue;
            group = entry.Key;
            break;
        }
        var recipes = available.Where(r => r.Group == group).ToList();
        var recipe = recipes[_random.Next(recipes.Count)];
        scroll.RecipeId = recipe.Id;
        return recipe;
    }

    public string DescribeRecipe(AlchemyRecipe recipe, bool includeRequirements = true)
    {
        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("alchemy-recipe-products"));
        foreach (var (id, amount) in recipe.Products)
            text.AppendLine($"{ReagentName(id)}: {amount}");
        foreach (var (id, count) in recipe.EntityProducts)
            text.AppendLine($"{_prototypes.Index<Robust.Shared.Prototypes.EntityPrototype>(id).Name}: {count}");
        text.AppendLine(Loc.GetString("alchemy-recipe-ingredients"));
        foreach (var (id, amount) in recipe.Ingredients)
            text.AppendLine($"{ReagentName(id)}: {amount}");
        foreach (var (id, count) in recipe.Entities)
            text.AppendLine($"{_prototypes.Index<Robust.Shared.Prototypes.EntityPrototype>(id).Name}: {count}");
        if (includeRequirements && recipe.AllowImpurities)
        {
            text.AppendLine(Loc.GetString("alchemy-recipe-impurities"));
        }
        text.AppendLine(Loc.GetString("alchemy-recipe-steps"));
        for (var i = 0; i < recipe.Steps.Count; i++)
        {
            var step = _prototypes.Index<AlchemyOperationPrototype>(recipe.Steps[i]);
            text.AppendLine($"{i + 1}. {Loc.GetString(step.Name)}");
        }
        return text.ToString();
    }

    public PotionBookUserInterfaceState BookState(IEnumerable<string> ids)
    {
        var result = new PotionBookUserInterfaceState();
        if (!TryGetRoundState(out var state))
            return result;
        foreach (var id in ids)
        {
            var recipe = state.Recipes.FirstOrDefault(r => r.Id == id);
            if (recipe != null)
            {
                result.Recipes.Add(new PotionBookRecipe
                {
                    Description = DescribeRecipe(recipe, false),
                    Products = recipe.Products.Keys.ToList(),
                    EntityProducts = recipe.EntityProducts.Keys.ToList(),
                });
            }
        }
        return result;
    }
}
