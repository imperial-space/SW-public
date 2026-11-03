using Content.Shared.Imperial.Medieval.Alchemy;

namespace Content.Server.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyRoundComponent : Component
{
    public Dictionary<string, AlchemyIngredient> Ingredients = new();
    public List<AlchemyRecipe> Recipes = new();
    public int HistoryLimit = 1;
}
