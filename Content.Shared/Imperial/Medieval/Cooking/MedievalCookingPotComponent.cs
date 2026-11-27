using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

[RegisterComponent]
public sealed partial class MedievalCookingPotComponent : Component
{
    [DataField]
    public string ContainerId = "pot_ingredients";

    [DataField]
    public string SolutionId = "pot";

    [DataField]
    public int MaxIngredients = 8;

    [DataField]
    public int MaxPerProduct = 3;

    [DataField]
    public EntityWhitelist? IngredientWhitelist;

    [DataField]
    public EntityWhitelist? IngredientBlacklist;

    [DataField]
    public EntityWhitelist? BowlWhitelist;

    [DataField]
    public string BowlSolution = "food";

    [DataField]
    public ProtoId<ReagentPrototype> WaterReagent = "Water";

    // all times are in seconds
    [DataField]
    public float ReadyWindow = 90f;

    [DataField]
    public float WarnBefore = 30f;

    [DataField]
    public float MessCookTime = 60f;

    [DataField]
    public int MessMinIngredients = 2;

    [DataField]
    public int MessPortions = 2;

    [DataField]
    public int MessWater = 20;

    [DataField]
    public EntProtoId MessResult = "MedievalFoodMess";

    [DataField]
    public EntProtoId BurntResult = "MedievalFoodBurntMess";

    [DataField]
    public float MinPortionNutrition = 0.5f;

    [DataField]
    public float EmptyDelay = 2f;

    [DataField]
    public SoundSpecifier? ReadySound;

    [DataField]
    public SoundSpecifier? WarnSound;

    [DataField]
    public SoundSpecifier? ServeSound;

    // players see water in measures of this size
    [DataField]
    public int WaterMeasure = 20;

    [DataField]
    public SoundSpecifier? AddSound;

    [DataField]
    public SoundSpecifier? SimmerSound;

    [ViewVariables]
    public EntityUid? SimmerStream;

    [ViewVariables]
    public MedievalPotState State;

    [ViewVariables]
    public string? Recipe;

    [ViewVariables]
    public float Progress;

    [ViewVariables]
    public float ReadyTimer;

    [ViewVariables]
    public bool Warned;

    [ViewVariables]
    public int Portions;

    [ViewVariables]
    public float PortionNutrition = 1f;

    [ViewVariables]
    public EntProtoId? Result;

    public float TickTimer;

    public Container Ingredients = default!;
}
