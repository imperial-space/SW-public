using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent, Access(typeof(AncientNocturneSpawnRuleSystem))]
public sealed partial class AncientNocturneSpawnRuleComponent : Component
{
    [DataField]
    public int SpawnCount = 3;

    [DataField]
    public EntProtoId SpawnerPrototype = "MedievalAncientNocturneGhostRoleSpawner";

    [DataField]
    public ProtoId<AntagPrototype> AntagPrototype = "AncientNocturne";

    [DataField]
    public TimeSpan InquisitionDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public EntProtoId InquisitionLeaderSpawnerPrototype = "MedievalHellfireInquisitionLeaderGhostRoleSpawner";

    [DataField]
    public EntProtoId InquisitionKnightSpawnerPrototype = "MedievalHellfireInquisitionKnightGhostRoleSpawner";

    [DataField]
    public EntProtoId InquisitionChaplainSpawnerPrototype = "MedievalHellfireInquisitionChaplainGhostRoleSpawner";

    [DataField]
    public int InquisitionKnightCount = 4;

    [DataField]
    public float InquisitionSpawnOffset = 1f;
}
