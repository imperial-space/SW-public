using Content.Shared.Friends.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class FriendsComponent : Component
    {

        [DataField]
        [AutoNetworkedField]
        public ProtoId<MedievalFactionPrototype> Faction { get; set; } = string.Empty;

        [AutoNetworkedField]
        public FactionMemberData MemberData = new();
    }
}
