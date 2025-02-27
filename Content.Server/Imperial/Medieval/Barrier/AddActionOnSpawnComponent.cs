namespace Content.Server.AddActionOnSpawn.Components
{
    [RegisterComponent]
    public sealed partial class AddActionOnSpawnComponent : Component
    {
        [DataField]
        public HashSet<string> Actions { get; set; } = new();
    }
}
