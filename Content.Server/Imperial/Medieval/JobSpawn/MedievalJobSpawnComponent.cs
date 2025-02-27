
namespace Content.Server.MedievalJobSpawn.Components
{
    [RegisterComponent]
    public sealed partial class MedievalJobSpawnComponent : Component
    {
        [DataField]
        public string SpawnType = "Default";

        [DataField]
        public bool Enabled = true;
    }

    [RegisterComponent]
    public sealed partial class MedievalJobPointComponent : Component
    {
        [DataField]
        public string SpawnType = "Default";

        [DataField]
        public bool Enabled = true;
    }
}
