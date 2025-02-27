namespace Content.Server.NeedSleep.Components
{
    [RegisterComponent]
    public sealed partial class NeedSleepRaceModifierComponent : Component
    {
        [DataField]
        public float Modifier = 1f;
    }
}
