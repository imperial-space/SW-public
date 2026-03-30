using System.Numerics;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.Client.Imperial.Medieval.ShipDrowning;

public sealed class ClientShipDrowningSystem : EntitySystem
{
    private const float VisualDrownIncreaseSmoothing = 0.95f;
    private const float VisualDrownDecreaseSmoothing = 1.45f;
    private const float WaterDriftSmoothing = 1.25f;
    private const float MaxWaterDrift = 0.22f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var driftLerp = 1f - MathF.Exp(-frameTime * WaterDriftSmoothing);
        var enumerator = EntityQueryEnumerator<ShipDrowningComponent, TransformComponent>();

        while (enumerator.MoveNext(out var uid, out var drowning, out var xform))
        {
            if (!drowning.VisualDataInitialized)
            {
                drowning.VisualDataInitialized = true;
                drowning.VisualDrownLevel = drowning.DrownLevel;
                drowning.VisualWaterOffset = Vector2.Zero;
            }

            var drownSmoothing = drowning.DrownLevel >= drowning.VisualDrownLevel
                ? VisualDrownIncreaseSmoothing
                : VisualDrownDecreaseSmoothing;
            var drownLerp = 1f - MathF.Exp(-frameTime * drownSmoothing);
            drowning.VisualDrownLevel = MathHelper.Lerp(drowning.VisualDrownLevel, drowning.DrownLevel, drownLerp);
            if (MathF.Abs(drowning.VisualDrownLevel - drowning.DrownLevel) < 0.01f)
                drowning.VisualDrownLevel = drowning.DrownLevel;

            var targetDrift = Vector2.Zero;

            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                targetDrift = (-xform.WorldRotation).RotateVec(-physics.LinearVelocity) * 0.034f;
                var driftLength = targetDrift.Length();

                if (driftLength > MaxWaterDrift)
                    targetDrift *= MaxWaterDrift / driftLength;
            }

            drowning.VisualWaterOffset = Vector2.Lerp(drowning.VisualWaterOffset, targetDrift, driftLerp);
        }
    }
}
