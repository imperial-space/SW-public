using System.Collections.Generic;

namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierRiftComponent : Component
    {
        [DataField]
        public string Element = "earth";

        [DataField]
        public bool GuardiansSpawned;

        [DataField]
        public List<EntityUid> Guardians = new();

        [DataField]
        public EntityUid? Spawner;
    }
}
