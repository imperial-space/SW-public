using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - what a cooking pot makes out of what lies in it. The pot has to hold exactly these
///     ingredients (in any order) and enough water.
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
    ///     One portion, already in a bowl.
    /// </summary>
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
///     One line of a recipe: <see cref="Count"/> items of an exact prototype, or of anything with a tag.
/// </summary>
[DataDefinition]
public sealed partial class MedievalCookIngredient
{
    [DataField]
    public EntProtoId? Id;

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
