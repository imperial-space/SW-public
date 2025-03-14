using Content.Shared.Friends.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Friends.Components;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FriendsComponent : Component
{

    [DataField]
    [AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype> Faction { get; set; } = string.Empty;

    [AutoNetworkedField]
    public EntityUid? FactionMenuActionEntity;

    [DataField]
    public EntProtoId FactionMenuAction = "BaseFactionMenuAction";

    [DataField]
    public int Priority = 1;

    [DataField]
    [AutoNetworkedField]
    public FactionMenuAccess MenuAccess = FactionMenuAccess.None;

    [AutoNetworkedField]
    public int MemberID = 0;
}

[Serializable, NetSerializable]
public enum FactionMenuAccess : byte
{
    None,
    Group,
    Full
}
