using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - what a cooking pot makes out of what lies in it. The pot has to hold every core
///     ingredient at least once (in any order) and enough water. Anything else in it has to be one of the extras.
/// </summary>
[Prototype("medievalCookRecipe")]
public sealed partial class MedievalCookRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<MedievalCookIngredient> Ingredients = new();

    /// <summary>
    ///     Units of water the pot must hold. All of it is used up.
    /// </summary>
    [DataField]
    public int Water = 60;

    /// <summary>
    ///     Seconds on a lit fire.
    /// </summary>
    [DataField(required: true)]
    public float CookTime;

    [DataField]
    public int Portions = 4;

    /// <summary>
    ///     Each one makes a portion more, up to <see cref="MaxPortions"/>.
    /// </summary>
    [DataField]
    public List<EntProtoId> Extras = new();

    [DataField]
    public int MaxPortions = 6;

    [DataField]
    public float ExtraSatietyLoss = 0.1f;

    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>
    ///     Played when a portion is ladled out; the pot's own sound is used when this is not set.
    /// </summary>
    [DataField]
    public SoundSpecifier? ServeSound;

    /// <summary>
    ///     Thick dishes have no fitting ladle sound, so they are served quietly.
    /// </summary>
    [DataField]
    public bool SilentServe;
}

/// <summary>
///     One line of a recipe: an exact prototype, any prototype from a list, or anything with a tag.
///     <see cref="Count"/> is the full amount: one is enough to cook, the full core gives all the portions.
/// </summary>
[DataDefinition]
public sealed partial class MedievalCookIngredient
{
    [DataField]
    public EntProtoId? Id;

    [DataField]
    public List<EntProtoId> Ids = new();

    [DataField]
    public ProtoId<TagPrototype>? Tag;

    /// <summary>
    ///     Items with any of these tags do not count even if they have <see cref="Tag"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> ExcludeTags = new();

    [DataField]
    public int Count = 1;
}
