using Robust.Shared.Prototypes;

namespace Content.Server.Nocturn;

[RegisterComponent]
public sealed partial class NocturnBloodFoodComponent : Component
{
    [DataField]
    public float BloodRestore = 15f;

    [DataField]
    public float BloodLevelCap = 200f;

    [DataField]
    public float EatDuration = 1f;

    [DataField]
    public EntProtoId BloodParticlesPrototype = "BloodParticles";
}
