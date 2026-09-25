using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Skill-owned state. Revoking perks must not remove racial or professional components.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SkillProgressionComponent : Component
{
    [DataField(serverOnly: true)] public Dictionary<MobState, FixedPoint2> BaseHealthThresholds = new();
    [DataField(serverOnly: true)] public float? BaseSoftCritThreshold;
    [DataField(serverOnly: true)] public float? BaseStamina;
    [DataField(serverOnly: true)] public List<string> RandomLanguages = new();
    [DataField(serverOnly: true)] public bool LanguagesSelected;
    [DataField(serverOnly: true)] public Dictionary<EntityUid, TimeSpan> HiddenTheftUntil = new();
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan NextDodge;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan NextHeavyHit;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan JumpRecharge;
    [DataField, AutoNetworkedField] public int JumpsUsed;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan ControlProtectionUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan ConvertedKnockdownUntil;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField] public TimeSpan JumpUntil;
    public float ConvertedSlowdown;
}
