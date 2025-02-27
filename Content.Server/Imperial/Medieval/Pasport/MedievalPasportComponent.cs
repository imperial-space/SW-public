namespace Content.Server.MedievalPasport.Components
{
    [RegisterComponent]
    public sealed partial class MedievalPasportComponent : Component
    {
        [DataField]
        public string PersonName = "none";
        [DataField]
        public string PersonGender = "none";
        [DataField]
        public string PersonAge = "none";
        [DataField]
        public string PersonJob = "none";
        [DataField]
        public string PersonRace = "none";

    }
}
