using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[Serializable, NetSerializable]
public sealed class PotionBookUserInterfaceState : BoundUserInterfaceState
{
    public List<PotionBookRecipe> Recipes = new();
}

[Serializable, NetSerializable]
public sealed class PotionBookRecipe
{
    public string Description = string.Empty;
    public List<string> Products = new();
    public List<string> EntityProducts = new();
}
