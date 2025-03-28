using Content.Shared.Friends.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FactionDataContainerComponent : Component
{
    /// <summary>
    /// Участники и информация о них.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<int, FactionMemberData>> CachedMembers = new();

    /// <summary>
    /// Цели для каждой из групп каждой из фракций
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<FactionMemberGroup, string>> Objectives = new();
}

