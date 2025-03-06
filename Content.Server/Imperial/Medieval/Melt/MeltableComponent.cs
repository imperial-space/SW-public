using Robust.Shared.Player;
using Content.Shared.Damage;

namespace Content.Server.Melter.Components
{
    [RegisterComponent]
    public sealed partial class MeltableComponent : Component
    {
        [DataField]
        public int MeltLevel = 0;
        [DataField]
        public int MaxMeltLevel = 3;
        [DataField]
        public bool Enabled = true;
        [DataField]
        public int ResourceCount = 1;
        [DataField]
        public string ResourceName = "MedievalIronIngot";

    }
}
