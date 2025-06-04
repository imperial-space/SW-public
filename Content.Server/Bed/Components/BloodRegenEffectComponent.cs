namespace Content.Server.Bed.Components
{
    [RegisterComponent]
    public sealed partial class BloodRegenEffectComponent : Component
    {
        [DataField("regenerationRate")]
        public float RegenerationRate = 1.0f; // Единиц крови в секунду
    }
}
