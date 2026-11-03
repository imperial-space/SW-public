using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalRandomChemistryRecipeComponent : Component
{
    [DataField]
    public Dictionary<string, float> Weights = new();
    [DataField]
    public string? RecipeId;
}
