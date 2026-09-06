using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules.TraderSpawn;

[RegisterComponent]
public sealed partial class TraderSpawnRuleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<JobPrototype> Job;

    [ViewVariables]
    public EntityUid? Performer;
}
