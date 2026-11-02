using System;
using System.Collections.Generic;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Medieval.Prediction;

/// <summary>
/// Reads server movement history as completed discrete snapshots. The queue is
/// chronological; several changes within one simulation tick share a timestamp.
/// </summary>
public static class MedievalLagCompensationHistory
{
    /// <summary>
    /// Selects the final recorded pose at or before the requested snapshot time.
    /// A gap means the last pose remained in effect, rather than motion toward a
    /// later sample. No interpolation or extrapolation is performed here.
    /// </summary>
    public static bool TryGetPose(
        Queue<(TimeSpan Time, EntityCoordinates Coordinates, Angle Rotation)> history,
        TimeSpan requestedTime,
        out EntityCoordinates coordinates,
        out Angle rotation)
    {
        coordinates = EntityCoordinates.Invalid;
        rotation = Angle.Zero;
        var found = false;

        foreach (var sample in history)
        {
            if (sample.Time > requestedTime)
                break;

            coordinates = sample.Coordinates;
            rotation = sample.Rotation;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Retains the final sample at or before the cutoff, plus every newer sample.
    /// The retained anchor does not extend the caller's permitted rewind window.
    /// </summary>
    public static void Prune(
        Queue<(TimeSpan Time, EntityCoordinates Coordinates, Angle Rotation)> history,
        TimeSpan cutoff)
    {
        while (history.Count > 1)
        {
            // Queue's concrete enumerator avoids a LINQ allocation or rebuilding
            // the queue. Only remove a pose when its successor is also old enough.
            using (var samples = history.GetEnumerator())
            {
                samples.MoveNext();
                samples.MoveNext();
                if (samples.Current.Time > cutoff)
                    break;
            }

            history.Dequeue();
        }
    }
}
