using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

[Prototype("medievalCookRecipe")]
public sealed partial class MedievalCookRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<MedievalCookIngredient> Ingredients = new();

    [DataField]
    public int Water = 60;

    // seconds on a lit fire
    [DataField(required: true)]
    public float CookTime;

    [DataField]
    public int Portions = 4;

    [DataField]
    public List<EntProtoId> Extras = new();

    [DataField]
    public int MaxPortions = 6;

    [DataField]
    public float ExtraSatietyLoss = 0.1f;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public SoundSpecifier? ServeSound;

    [DataField]
    public bool SilentServe;
}

[DataDefinition]
public sealed partial class MedievalCookIngredient
{
    [DataField]
    public EntProtoId? Id;

    [DataField]
    public List<EntProtoId> Ids = new();

    [DataField]
    public ProtoId<TagPrototype>? Tag;

    [DataField]
    public List<ProtoId<TagPrototype>> ExcludeTags = new();

    [DataField]
    public int Count = 1;
}
