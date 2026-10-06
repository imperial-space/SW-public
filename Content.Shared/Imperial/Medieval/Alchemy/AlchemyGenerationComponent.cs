namespace Content.Shared.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyGenerationComponent : Component
{
    [DataField] public Dictionary<string, int> AspectMinimumTiers = new()
    {
        ["AlchemyWater"] = 1,
        ["AlchemyEarth"] = 1,
        ["AlchemyFire"] = 1,
        ["AlchemyLight"] = 1,
        ["AlchemyDarkness"] = 3,
    };
}
