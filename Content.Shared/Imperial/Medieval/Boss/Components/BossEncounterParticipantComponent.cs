using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossEncounterParticipantComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Boss;
}
