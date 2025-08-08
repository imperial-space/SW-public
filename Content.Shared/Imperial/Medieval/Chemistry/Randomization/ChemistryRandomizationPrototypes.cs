using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

[Prototype("chemRandom")]
public sealed partial class ChemistryRandomizationGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Целевые зелья
    /// </summary>
    [DataField(required: true)]
    public List<string> Potions = new();

    /// <summary>
    /// Используемые реагенты
    /// </summary>
    [DataField]
    public List<string> Reagents = new()
    {
        "MedievalFlowerLipad",
        "MedievalFlowerVetr",
        "MedievalFlowerCryo",
        "MedievalLootCrabDevil",
        "MedievalLootCrab",
        "MedievalLootHunter",
        "MedievalLootEgg",
        "MedievalLootHog",
        "MedievalLootGoblin",
        "MedievalLootAnt",
        "MedievalLootDevilMouse",
        "Blood",
        "DemonsBlood",
        "MedievalFlowerLava",
        "MedievalMushroom1",
        "MedievalMushroom2",
        "MedievalMushroom3"
    };

    /// <summary>
    /// Описания зелий
    /// </summary>
    [DataField]
    public List<string> Descriptions = new()
    {
        "reagent-physical-desc-opaque",
        "reagent-physical-desc-cloudy",
        "reagent-physical-desc-glowing",
        "reagent-physical-desc-syrupy",
        "reagent-physical-desc-cold",
        "reagent-physical-desc-acidic",
        "reagent-physical-desc-bubbling",
        "reagent-physical-desc-strong-smelling"
    };

    /// <summary>
    /// Вкусы зелий
    /// </summary>
    [DataField]
    public List<string> Flavors = new()
    {
        "alcohol",
        "ambrosia",
        "medicine",
        "cold",
        "funny",
        "sharp",
        "mushroom",
        "Pyrotechnic",
        "sweet",
        "metallic"
    };

    [DataField]
    public List<Color> Colors = new();

    /// <summary>
    /// Звуки, проигрываемые при реакции
    /// </summary>
    [DataField]
    public List<SoundSpecifier> Sounds = new()
    {
        new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg")
    };

    /// <summary>
    /// Эффекты, которые могут добавиться к реакции
    /// </summary>
    [DataField]
    public EntityEffectRandom EffectRandom = new();

    /// <summary>
    /// Другие рандом группы, которые будут использованы
    /// </summary>
    [DataField]
    public List<string> UsedGroups = new();

    /// <summary>
    /// Число реагентов для генерации зелья
    /// </summary>
    [DataField]
    public int Reactants = 2;

    /// <summary>
    /// Рамки для минимальной температуры. null для дефолтного значения
    /// </summary>
    [DataField]
    public MinMax? MinTemperature;

    /// <summary>
    /// Рамки для максимальной температуры. null для дефолтного значения
    /// </summary>
    [DataField]
    public MinMax? MaxTemperature;
}

[DataDefinition]
public sealed partial class EntityEffectRandom
{
    [DataField]
    public float Probability = 0.2f;

    [DataField]
    public int Count = 1;

    [DataField(required: true)]
    public List<EntityEffect> Effects = new();
}
