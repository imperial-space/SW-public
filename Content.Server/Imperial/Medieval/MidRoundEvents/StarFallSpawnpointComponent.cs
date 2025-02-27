namespace Content.Server.MagicBarrier.Components
{
    [RegisterComponent]
    public sealed partial class StarFallComponent : Component
    {
        [DataField]
        public bool Active = true;

        [DataField]
        public string Side = "в небе";
    }
}
