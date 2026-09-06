using Robust.Shared.Prototypes;

namespace Content.Server.Nocturn;

[RegisterComponent]
public sealed partial class BloodRubyComponent : Component
{
    [DataField]
    public float TotalBlood;

    [DataField]
    public float BloodForFullGlow = 5000f;

    [DataField]
    public Color EmptyColor = Color.White;

    [DataField]
    public Color FullColor = Color.Red;

    [DataField]
    public float MinimumLightRadius = 0.1f;

    [DataField]
    public float MaximumLightRadius = 10f;

    [DataField]
    public float MinimumLightEnergy = 0.1f;

    [DataField]
    public float MaximumLightEnergy = 10f;
}

[RegisterComponent]
public sealed partial class BloodRubyOwnerComponent : Component
{
    [DataField]
    public EntProtoId BloodRubyPrototype = "MedievalBloodRuby";

    [DataField]
    public float MinimumBloodLevel = 50f;

    [DataField]
    public float BloodPerDonation = 50f;

    [DataField]
    public float DonationDuration = 1f;

    [DataField]
    public EntProtoId BloodParticlesPrototype = "BloodParticles";

    [DataField]
    public float EmergencyTeleportCastDuration = 1f;

    [DataField]
    public float EmergencyTeleportBlockedCooldown = 15f;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? BloodRuby;
}
