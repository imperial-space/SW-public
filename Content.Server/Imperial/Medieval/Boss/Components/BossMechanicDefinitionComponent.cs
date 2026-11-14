using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public enum BossActionSlot : byte
{
    Pattern,
    Sequence,
    Traversal,
    Execution,
    Custom,
}

[RegisterComponent]
public sealed partial class BossMechanicDefinitionComponent : Component
{
    [DataField(required: true)] public string Encounter = string.Empty;                  // mechanic group
    [DataField] public EntProtoId? BossPrototype;                                // bind presentation to the boss independently of its flow
    [DataField(required: true)] public string Feature = string.Empty;            // name of the installed mechanic
    [DataField] public BossActionSlot Slot;                                      // controller used for this mechanic
    [DataField] public List<BossTelegraphPattern> Patterns = new();
    [DataField] public List<BossTraversalKind> Traversals = new();
    [DataField] public BossAttack? Attack;
    [DataField] public ProtoId<BossTelegraphStylePrototype>? Style;
    [DataField] public EntProtoId? AutoProjectilePrototype;
    [DataField] public EntProtoId? BlueEmitterPrototype;
    [DataField] public EntProtoId? PurpleEmitterPrototype;
    [DataField] public ComponentRegistry Components = new();                     // components added to matching bosses
}
