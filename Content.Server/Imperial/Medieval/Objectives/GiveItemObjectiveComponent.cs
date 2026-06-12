namespace Content.Server.Imperial.Medieval;

[RegisterComponent]
public sealed partial class GiveItemObjectiveComponent : Component
{
    [DataField]
    public List<string> Objectives = new List<string> {
        "MedievalGetBible",
        "MedievalGetMedievalWeaponCrossbow",
        "MedievalGetMedievalClothingShoesSpider",
        "MedievalGetMedievalWineskin",
        "MedievalGetMedievalGeigerCounter",
        "MedievalGetShardCrystalRed",
        "MedievalGetMedievalLootGoblin",
        "MedievalGetMedievalClothingNeckCloakGoblin",
        "MedievalGetMedievalLootBear",
        "MedievalGetMedievalShoesSpeed",
        "MedievalGetMedievalHolySalt",
        "MedievalGetMedievalClothingUniformJumpsuitShitCloth7",
        "MedievalGetMedievalClothingFeetSteel",
        "MedievalGetMedievalIronGloves",
        "MedievalGetMedievalClothingOuterArmorUpIron",
        "MedievalGetMedievalClothingHeadHelmetNazal",
        "MedievalGetMedievalPersonalAI",
        "MedievalGetMedievalDaggerT3Bone",
        "MedievalGetFoodMeatSalami",
        "MedievalGetClothingHeadHatCowboyBrown",
        "MedievalGetDiceBag",
        "MedievalGetFoodApple",
        "MedievalGetLeavesCannabis",
        "MedievalGetLeavesTobacco",
        "MedievalGetSmokingPipe",
        "MedievalGetMedaljonDef",
        "MedievalGetMedaljonFlashImmune",
        "MedievalGetMedaljonExpl",
        "MedievalGetMedaljonSpeed",
        "MedievalGetMedaljonNoslip",
        "MedievalGetMedaljonDark",

        "MedievalGetLeavesCannabis",
        "MedievalGetLeavesTobacco",
        "MedievalGetMedievalClothingOuterArmorUpIron",
        "MedievalGetMedievalClothingNeckCloakGoblin",
        "MedievalGetShardCrystalRed"
        };

    [DataField]
    public int MinObjectives = 3;

    [DataField]
    public int MaxObjectives = 4;

    [DataField]
    public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);
    [DataField]
    public bool Predicted = false;
}
