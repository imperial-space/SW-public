using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Water = "Water";

    [ValidatePrototypeId<ReagentPrototype>] // imperial blood evaporation start
    private const string Blood = "Blood";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string DemonsBlood = "DemonsBlood";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Sap = "Sap";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Protein = "Protein";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Nutriment = "Nutriment";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Milk = "Milk";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Wine = "Wine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Vodka = "Vodka";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Mead = "Mead";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string RedMead = "RedMead";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Beer = "Beer";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Rum = "Rum";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Moonshine = "Moonshine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Ale = "Ale";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Arithrazine = "Arithrazine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string DexalinPlus = "DexalinPlus";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Impedrezene = "Impedrezene";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Spaceacillin = "Spaceacillin";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string UnstableMutagen = "UnstableMutagen";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string SpaceDrugs = "SpaceDrugs";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Nocturine = "Nocturine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Omnizine = "Omnizine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Stimulants = "Stimulants";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string MuteToxin = "MuteToxin";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Thrivenin = "Thrivenin";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Phalanximine = "Phalanximine";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string ChlorineTrifluoride = "ChlorineTrifluoride";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Iron = "Iron";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string TranexamicAcid = "TranexamicAcid";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string RobustHarvest = "RobustHarvest";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Ethanol = "Ethanol";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Vitamin = "Vitamin";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Vomit = "Vomit";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Amatoxin = "Amatoxin";

    public static readonly string[] EvaporationReagents =
{
    "Water",
    "Blood",
    "DemonsBlood",
    "Sap",
    "Ethanol",
    "Protein",
    "Vomit",
    "Amatoxin",
    "Vitamin",
    "Nutriment",
    "Milk",
    "Wine",
    "Vodka",
    "Mead",
    "RedMead",
    "Beer",
    "Rum",
    "Moonshine",
    "Ale",
    "Arithrazine",
    "DexalinPlus",
    "Impedrezene",
    "Spaceacillin",
    "UnstableMutagen",
    "SpaceDrugs",
    "Nocturine",
    "Omnizine",
    "Stimulants",
    "MuteToxin",
    "Thrivenin",
    "Phalanximine",
    "ChlorineTrifluoride",
    "Iron",
    "TranexamicAcid",
    "RobustHarvest",
    // Imperial Medieval Chemistry Begin
    "medievalexplosionclone",
    "medievalhealburnclone",
    "medievalhealbloodandoxygenclone",
    "medievalacidclone",
    "medievalradiationclone",
    "medievaldrughealmechclone",
    "medievalvomitclone",
    "Ammoniaclone",
    "PlantBGoneclone",
    "Left4Zedclone",
    "Diethylamineclone",
    "medievalslowclone",
    "medievalfastclone",
    "medievalstaminaclone",
    "medievalsmokeclone",
    "Antiholclone",
    "medievalcryoexplosionclone",
    "medievalhealslashclone",
    "medievalhealpiercingclone",
    "medievalhealbluntclone",
    "medievalmuteclone",
    "medievalblindclone",
    "medievaljitterclone",
    "medievalsleepclone",
    "medievaligniteclone",
    "medievalhealandstopbloodclone",
    "medievaladvancedhealeverythingclone",
    "medievaladvancedhealslashclone",
    "medievaladvancedhealpiercingclone",
    "medievaladvancedhealbluntclone",
    "medievaladvancedhealburnclone",
    "medievaladvancedacidradiationclone",
    "medievaladvancedfastclone",
    "medievaladvancedslowclone",
    "medievaladvancedpacifismclone",
    "medievaladvancedteleportclone",
    "medievaladvancedhealandstopbloodclone",
    "medievaladvancedstimulantclone",
    "medievaladvancedigniteclone",
    "medievaladvancednocturnbloodclone",
    "medievaladvancedsmokeandexplosionclone",
    "medievalbestexplosionclone",
    "medievalbesthealclone",
    "medievalbestpoisonclone",
    "medievalbeststimulantclone",
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
    "MedievalFlowerLava",
    "MedievalMushroom1",
    "MedievalMushroom2",
    "MedievalMushroom3"
    // Imperial Medieval Chemistry End
}; // imperial reagents evaporation end

    public bool CanFullyEvaporate(Solution solution)
    {
        return solution.GetTotalPrototypeQuantity(EvaporationReagents) == solution.Volume;
    }
}
