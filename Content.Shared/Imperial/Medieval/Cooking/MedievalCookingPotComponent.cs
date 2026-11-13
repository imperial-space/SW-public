using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - a pot that cooks what is put in it while its fire burns. Ingredients lie in a container,
///     water is a solution. When a recipe matches, it cooks by itself, and left too long on the fire it burns.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalCookingPotComponent : Component
{
    [DataField]
    public string ContainerId = "pot_ingredients";

    [DataField]
    public string SolutionId = "pot";

    [DataField]
    public int MaxIngredients = 4;

    [DataField]
    public EntityWhitelist? IngredientWhitelist;

    /// <summary>
    ///     What never goes in as an ingredient even if it passes the whitelist, e.g. bottles you pour from.
    /// </summary>
    [DataField]
    public EntityWhitelist? IngredientBlacklist;

    /// <summary>
    ///     What counts as an empty bowl a portion can be ladled into.
    /// </summary>
    [DataField]
    public EntityWhitelist? BowlWhitelist;

    /// <summary>
    ///     Seconds a finished dish can stay on a lit fire before it burns.
    /// </summary>
    [DataField]
    public float ReadyWindow = 90f;

    /// <summary>
    ///     The smell of burning starts this many seconds before the dish burns.
    /// </summary>
    [DataField]
    public float WarnBefore = 30f;

    /// <summary>
    ///     Ingredients that match no recipe turn into a poor stew after this many seconds.
    /// </summary>
    [DataField]
    public float MessCookTime = 60f;

    /// <summary>
    ///     A lone ingredient is not a dish: the pot only starts a poor stew from this many ingredients.
    /// </summary>
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

    /// <summary>
    ///     Seconds it takes to pour everything out, so a stray click doesn't throw a finished dish away.
    /// </summary>
    [DataField]
    public float EmptyDelay = 2f;

    [DataField]
    public SoundSpecifier? ReadySound;

    [DataField]
    public SoundSpecifier? WarnSound;

    [DataField]
    public SoundSpecifier? ServeSound;

    /// <summary>
    ///     Water is shown to players in measures of this many units, not in raw units.
    /// </summary>
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

    /// <summary>
    ///     The recipe the current ingredients match; progress belongs to it and is dropped when it changes.
    /// </summary>
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
    public EntProtoId? Result;

    public float TickTimer;

    public Container Ingredients = default!;
}
