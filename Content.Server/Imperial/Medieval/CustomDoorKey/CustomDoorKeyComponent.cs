namespace Content.Server.CustomDoorKey.Components
{
    [RegisterComponent]
    public sealed partial class CustomDoorKeyComponent : Component
    {
        [DataField]
        public EntityUid? linkedKey;
    }
}
