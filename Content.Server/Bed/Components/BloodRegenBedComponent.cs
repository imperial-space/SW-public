namespace Content.Server.Bed.Components
{
    [RegisterComponent]
    public sealed partial class BloodRegenBedComponent : Component
    {
        [DataField("regenerationRate")]
        public float RegenerationRate = 1.0f;
    }
}
