using Robust.Shared.GameStates;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FightingBossComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Boss;
    [DataField, AutoNetworkedField]
    public bool UseEncounterHud;
}
