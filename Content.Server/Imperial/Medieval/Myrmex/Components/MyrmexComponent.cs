using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Myrmex.Components;

[RegisterComponent]
public sealed partial class MyrmexComponent : Component
{
    [DataField]
    public List<EntProtoId> Actions;

    [DataField]
    public float CurrentSpeedMultiplier = 1;

    [DataField]
    public bool StealthActive;

    [DataField]
    public bool StunActive;

    [DataField]
    public TimeSpan StunDuration;

    [DataField]
    public bool ArmorActive;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> StandardArmorProto;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> ActiveArmorProto;

    [DataField]
    public float ActiveArmorSpeedMultiplier;

    [DataField]
    public List<(FixedPoint2 threshold, MobState state)>? BaseHealthThresholds = null;
}
