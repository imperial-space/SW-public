namespace Content.Shared.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class HelmComponent : Component
{
    [DataField("helmRotation")]
    public float HelmRotation;

    [DataField("rotationStep")]
    public float RotationStep = 5f;

    [DataField("steeringAngleForMaxTurn")]
    public float SteeringAngleForMaxTurn = 45f;

    [DataField("turnImpulseScalar")]
    public float TurnImpulseScalar = 20f;

    [DataField("minMotionFactor")]
    public float MinMotionFactor = 0.25f;

    [DataField("minShipWeight")]
    public float MinShipWeight = 10f;
}
