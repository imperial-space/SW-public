namespace Content.Server.SpikeTrap.Components
{
    [RegisterComponent]
    public sealed partial class MedievalSpikeTargetComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField]
        public int HitCount = 0;

        [DataField]
        public int Screams = 0;

        [DataField]
        public int Potions = 0;

        [DataField]
        public int Lockpicks = 0;

        [DataField]
        public int Crafts = 0;

        [DataField]
        public int Diggs = 0;

        [DataField]
        public int Alcohol = 0;
    }
}
