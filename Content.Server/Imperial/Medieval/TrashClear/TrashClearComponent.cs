
namespace Content.Server.TrashClear.Components
{
    [RegisterComponent]
    public sealed partial class TrashClearComponent : Component
    {
        [DataField]
        public bool OnTheGround = false;

        [DataField]
        public bool Enabled = false;

        [DataField]
        public bool Safe = false;

        [DataField]
        public TimeSpan WillBeDespawnedTime = TimeSpan.FromSeconds(0f);

        [DataField]
        public TimeSpan DespawnTime = TimeSpan.FromSeconds(900f);
    }
}
