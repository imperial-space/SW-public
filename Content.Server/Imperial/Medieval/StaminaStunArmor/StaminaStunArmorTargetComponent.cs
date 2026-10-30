using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System;

namespace Content.Server.Imperial.Medieval.StunArmor;

[RegisterComponent]
public sealed partial class StaminaStunArmorTargetComponent : Component
{
    [DataField]
    public int Combo = 0;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastCritTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan ComboWindow = TimeSpan.FromMinutes(1);

    [DataField]
    public float EfficiencyDropPerCombo = 0.25f;
}

