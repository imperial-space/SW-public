using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.SkillCheck;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillCheckDieComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Result = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan RollStartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.8);

    [DataField(serverOnly: true)]
    public TimeSpan ResultDuration = TimeSpan.FromSeconds(3);

    [DataField(serverOnly: true)]
    public EntityUid? Performer;

    [DataField(serverOnly: true)]
    public ProtoId<SkillPrototype>? Skill;

    [DataField(serverOnly: true)]
    public int Modifier;

    [DataField(serverOnly: true)]
    public Color CriticalFailureColor = Color.Red;

    [DataField(serverOnly: true)]
    public Color CriticalSuccessColor = Color.Green;
}
