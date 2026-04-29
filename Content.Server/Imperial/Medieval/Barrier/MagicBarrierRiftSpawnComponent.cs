namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class MagicBarrierRiftSpawnComponent : Component
    {
        [DataField]
        public bool Occupied;
    }
}
