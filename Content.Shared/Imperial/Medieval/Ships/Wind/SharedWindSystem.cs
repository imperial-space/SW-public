using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Medieval.Ships.Wind;

/// <summary>
/// This handles wind force calculations for sails.
/// </summary>
public sealed class SharedWindSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        // Initialization logic here if needed
    }

    /// <summary>
    /// Calculates the aerodynamic forces acting on a sail due to wind.
    /// </summary>
    /// <param name="shipPosition">World position of the ship.</param>
    /// <param name="shipRotation">Current rotation of the ship in radians.</param>
    /// <param name="sailAngle">Angle of the sail relative to the ship's orientation (radians).</param>
    /// <param name="sailArea">Effective area of the sail (m²).</param>
    /// <param name="time">Current time for dynamic wind simulation.</param>
    /// <param name="airDensity">Density of air (default: 1.2 kg/m³).</param>
    /// <returns>A <see cref="WindEffect"/> containing force, torque, and wind data.</returns>
    public static WindEffect CalculateWindForce(
        Vector2 shipPosition,
        float shipRotation,
        float sailAngle,
        float sailArea,
        float time = 0f,
        float airDensity = 1.2f)
    {
        // Get wind vector at ship's position
        var windVector = GetWindField(shipPosition, time);
        var windStrength = windVector.Length();
        var windDirection = windVector.Normalized();

        // Absolute sail angle in world coordinates
        var absoluteSailAngle = shipRotation + sailAngle;

        // Angle between wind direction and sail normal
        var windAngle = MathF.Atan2(windDirection.Y, windDirection.X);
        var angleDiff = absoluteSailAngle - windAngle;
        var angleOfAttack = NormalizeAngle(angleDiff);

        // Lift and drag coefficients based on angle of attack
        var liftCoefficient = MathF.Sin(2f * angleOfAttack);
        var dragCoefficient = MathF.Abs(MathF.Cos(angleOfAttack));

        // Dynamic pressure
        var dynamicPressure = 0.5f * airDensity * windStrength * windStrength;

        // Force magnitudes
        var liftForce = liftCoefficient * dynamicPressure * sailArea;
        var dragForce = dragCoefficient * dynamicPressure * sailArea;

        // Sail orientation vectors
        Vector2 sailDirection = new(MathF.Cos(absoluteSailAngle), MathF.Sin(absoluteSailAngle)); // Along sail
        Vector2 sailNormal = new(-MathF.Sin(absoluteSailAngle), MathF.Cos(absoluteSailAngle));   // Perpendicular to sail

        // Force components
        var liftVector = sailNormal * liftForce;
        var dragVector = sailDirection * dragForce;

        // Total force
        var totalForce = liftVector + dragVector;

        // Torque calculation
        var leverArm = 1f; // Distance from center of mass to sail (can be parameterized)
        var torque = CalculateTorque(totalForce, absoluteSailAngle, leverArm);

        return new WindEffect
        {
            PushForce = totalForce,
            RotationTorque = angleDiff,
            WindStrength = windStrength,
            WindDirection = windDirection
        };
    }

    /// <summary>
    /// Normalizes an angle to the range [-π, π]
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI)
            angle -= MathF.Tau;
        while (angle < -MathF.PI)
            angle += MathF.Tau;
        return angle;
    }

    /// <summary>
    /// Simple procedural wind field simulation
    /// </summary>
    private static Vector2 GetWindField(Vector2 position, float time = 0f)
    {
        var x = position.X;
        var y = position.Y;

        var u = MathF.Sin(x / 2f + time) * MathF.Cos(y / 3f) + 0.5f * MathF.Sin(x / 5f + y / 7f);
        var v = MathF.Cos(x / 3f + time) * MathF.Sin(y / 2f) + 0.5f * MathF.Cos(x / 7f - y / 5f);

        return new Vector2(u, v);
    }

    /// <summary>
    /// Calculates rotational torque applied by wind force
    /// </summary>
    private static float CalculateTorque(Vector2 force, float sailAngle, float leverArm)
    {
        if (force.LengthSquared() < 0.001f)
            return 0f;

        var forceDirection = force.Normalized();
        Vector2 leverDirection = new(-MathF.Sin(sailAngle), MathF.Cos(sailAngle));

        // Cross product in 2D: F × r
        var crossProduct = forceDirection.X * leverDirection.Y - forceDirection.Y * leverDirection.X;

        return force.Length() * leverArm * crossProduct;
    }
}

/// <summary>
/// Represents the effect of wind on a sail.
/// </summary>
public struct WindEffect
{
    /// <summary>
    /// Net force applied to the ship in world coordinates.
    /// </summary>
    public Vector2 PushForce;

    /// <summary>
    /// Rotational torque around the ship's center.
    /// </summary>
    public float RotationTorque;

    /// <summary>
    /// Magnitude of the wind vector at this point.
    /// </summary>
    public float WindStrength;

    /// <summary>
    /// Direction of the wind (normalized).
    /// </summary>
    public Vector2 WindDirection;
}
