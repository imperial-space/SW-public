using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Revive;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KillsUntilReviveComponent : Component
{
    [DataField, AutoNetworkedField]
    public int RequiredKills = 12;

    [DataField, AutoNetworkedField]
    public int CurrentKills = 0;

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> KillsAlert = "KilledSpirits";

    [DataField, AutoNetworkedField]
    public EntProtoId EffectProto = "BlackHoleSpellCastEffectBeginner";
}
