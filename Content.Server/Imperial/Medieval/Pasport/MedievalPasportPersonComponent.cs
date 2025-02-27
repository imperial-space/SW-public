using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Server.MedievalPasport.Components
{
    [RegisterComponent]
    public sealed partial class MedievalPasportPersonComponent : Component
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
        public string Pasport = "MedievalPasportBase";

        [DataField]
        public EntityUid? PasportEntity = null;
    }
}
