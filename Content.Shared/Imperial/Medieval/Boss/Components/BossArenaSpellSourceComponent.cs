using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossArenaSpellSourceComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Player;
    [DataField, AutoNetworkedField]
    public EntityUid? Arena;
    [DataField, AutoNetworkedField]
    public BossArenaDamageKind DamageKind;
}
