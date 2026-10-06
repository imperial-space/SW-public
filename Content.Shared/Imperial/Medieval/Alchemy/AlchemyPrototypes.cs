using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[Prototype("alchemyOperation")]
public sealed partial class AlchemyOperationPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name = default!;
    [DataField(required: true)] public LocId StartMessage;
    [DataField(required: true)] public LocId EmptyMessage;
    [DataField(required: true)] public LocId RunningMessage;
    [DataField(required: true)] public LocId CompletionMessage;
    [DataField] public int Complexity = 1;
    [DataField] public int MinimumTier = 1;
    [DataField] public int Weight = 10;
    [DataField] public float Duration = 3;
    [DataField] public float? Temperature;
    [DataField] public bool Heating;
}

[DataDefinition]
public sealed partial class AlchemyIngredientRequirement
{
    [DataField] public string? Reagent;
    [DataField] public FixedPoint2? Amount;
}

[DataDefinition]
public sealed partial class AlchemyStep
{
    [DataField] public string? Operation;
}

[Prototype("alchemyRecipe")]
public sealed partial class AlchemyRecipePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<AlchemyRecipePrototype>))]
    public string[]? Parents { get; }
    [NeverPushInheritance, AbstractDataField] public bool Abstract { get; }
    [DataField(required: true)] public AlchemyRecipeTag Tag;
    [DataField] public bool Randomized;
    [DataField] public string Group = "EasyPack";
    [DataField] public int Tier = 1;
    [DataField] public List<AlchemyIngredientRequirement>? Ingredients;
    [DataField] public List<AlchemyStep>? Steps;
    [DataField] public Dictionary<string, int> Entities = new();
    [DataField] public Dictionary<string, FixedPoint2> Products = new();
    [DataField] public Dictionary<string, int> EntityProducts = new();
    [DataField] public bool? StrictRatio;
    [DataField] public bool? AllowImpurities;
    [DataField] public int IngredientCount = 2;
    [DataField] public int MinParts = 1;
    [DataField] public int MaxParts = 5;
    [DataField] public FixedPoint2? TotalAspectAmount;
    [DataField] public int ExpectedSteps = 3;
    [DataField] public int MaxComplexity = 8;
    [DataField] public int Priority;
    [DataField] public Dictionary<string, int> AspectWeights = new()
    {
        ["AlchemyWater"] = 10,
        ["AlchemyEarth"] = 10,
        ["AlchemyFire"] = 10,
        ["AlchemyLight"] = 10,
        ["AlchemyDarkness"] = 1,
    };
}

[Prototype("alchemyIngredient")]
public sealed partial class AlchemyIngredientPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public FixedPoint2 ReagentAmount = 10;
    [DataField] public int MinYield = 4;
    [DataField] public int MaxYield = 8;
    [DataField] public int AspectCount = 1;
    [DataField] public List<string> Solvents = new() { "Wine", "Water", "Ethanol", "Oil" };
    [DataField] public Dictionary<string, int> AspectWeights = new()
    {
        ["AlchemyWater"] = 10,
        ["AlchemyEarth"] = 10,
        ["AlchemyFire"] = 10,
        ["AlchemyLight"] = 10,
    };
}

public sealed class AlchemyRecipe
{
    public string Id = string.Empty;
    public string Group = string.Empty;
    public Dictionary<string, FixedPoint2> Ingredients = new();
    public List<string> Steps = new();
    public Dictionary<string, int> Entities = new();
    public Dictionary<string, FixedPoint2> Products = new();
    public Dictionary<string, int> EntityProducts = new();
    public bool StrictRatio;
    public bool AllowImpurities;
    public int Priority;
}

public sealed class AlchemyIngredient
{
    public FixedPoint2 ReagentAmount;
    public string Solvent = string.Empty;
    public Dictionary<string, FixedPoint2> Aspects = new();
}
