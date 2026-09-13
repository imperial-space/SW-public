using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Calendar;

[DataDefinition]
public sealed partial class CalendarWeightCurve : ISerializationHooks
{
    [DataField("points")]
    public List<Vector2> Points = new();

    void ISerializationHooks.AfterDeserialization()
    {
        Points.Sort((a, b) => a.X.CompareTo(b.X));
    }

    public float Evaluate(float day)
    {
        if (Points.Count == 0) return 1f;
        if (Points.Count == 1) return Points[0].Y;

        if (day <= Points[0].X) return Points[0].Y;
        if (day >= Points[^1].X) return Points[^1].Y;

        for (var i = 0; i < Points.Count - 1; i++)
        {
            var p1 = Points[i];
            var p2 = Points[i + 1];

            if (day >= p1.X && day <= p2.X)
            {
                var t = (day - p1.X) / (p2.X - p1.X);
                return p1.Y + (p2.Y - p1.Y) * t;
            }
        }

        return Points[^1].Y;
    }
}
