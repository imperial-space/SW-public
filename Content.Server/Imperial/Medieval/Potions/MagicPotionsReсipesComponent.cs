namespace Content.Server.MagicPotionsMaker.Components
{
    [RegisterComponent]
    public sealed partial class MagicPotionsRecipesComponent : Component
    {
        [DataField]
        public string CryoCryo = "None";
        [DataField]
        public string CryoVetr = "None";
        [DataField]
        public string CryoLipad = "None";
        [DataField]
        public string CryoLava = "None";
        [DataField]
        public string VetrVetr = "None";
        [DataField]
        public string VetrLipad = "None";
        [DataField]
        public string VetrLava = "None";
        [DataField]
        public string LipadLipad = "None";
        [DataField]
        public string LipadLava = "None";
        [DataField]
        public string LavaLava = "None";

        [DataField]
        public string[] Potions = {
            "MedievalImpedrezeneChemistryBottle",
            "MedievalBloodChemistryBottle",
            "MedievalRadChemistryBottle",
            "MedievalGigaHealChemistryBottle",
            "MedievalGigaHealChemistryBottle",
            "MedievalGeneticChemistryBottle",
            "MedievalSpaceChemistryBottle",
            "MedievalFireChemistryBottle",
            "MedievalDrugChemistryBottle",
            "MedievalMutagenChemistryBottle",
            "MedievalSleepChemistryBottle",
            "MedievalBaffChemistryBottle",
            "MedievalMuteChemistryBottle",
            "MedievalHealthChemistryBottle",
            "MedievalHealthChemistryBottle",
            "MedievalAntChemistryBottle"
            };

        // Cryo Vetr Lipad Lava
    }
}
