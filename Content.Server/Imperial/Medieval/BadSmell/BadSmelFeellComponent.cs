namespace Content.Server.BadSmell.Components
{
    [RegisterComponent]
    public sealed partial class BadSmellFeelComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField]
        public bool DescEnabled = true;
    }
}
