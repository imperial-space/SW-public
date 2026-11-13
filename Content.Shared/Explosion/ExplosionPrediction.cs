// Imperial medieval start
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Explosion;

/// <summary>Presentation identity captured before the exploding projectile is deleted.</summary>
[Serializable, NetSerializable]
public readonly record struct ExplosionPrediction(ProjectilePredictionKey Key, NetEntity Shooter);

/// <summary>Replaces the ordinary blast audio for its predicting shooter; never reports a client hit.</summary>
[Serializable, NetSerializable]
public sealed class ExplosionSoundEvent : EntityEventArgs
{
    public List<ProjectilePredictionKey> Keys = new();
    public MapCoordinates Epicenter;
    public SoundSpecifier Sound = default!;
    public bool Global;
}

/// <summary>Shared intensity accounting. The server supplies wall-aware tile counts.</summary>
public static class ExplosionPredictionMath
{
    public static void RaiseIntensity(List<float> intensity, List<int> tileCounts, float step, float maxIntensity,
        ref float remaining, ref int maxIndex)
    {
        for (var i = maxIndex; i < intensity.Count; i++)
        {
            var increase = MathF.Min(step, maxIntensity - intensity[i]);
            if (tileCounts[i] * increase >= remaining)
            {
                intensity[i] += remaining / tileCounts[i];
                remaining = 0;
                break;
            }
            intensity[i] += increase;
            remaining -= tileCounts[i] * increase;
            if (increase < step)
                maxIndex++;
        }
    }

}
// Imperial medieval end
