namespace Content.Server.Friends.Components
{
    [RegisterComponent]
    public sealed partial class FriendsComponent : Component
    {

        [DataField]
        public string Faction { get; set; } = string.Empty;

    }
}
