using Robust.Shared.GameStates;

namespace Content.Shared.Religion.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class ReligionMemberComponent : Component
    {
        [DataField]
        public string Religion = "none";
    }
}
