using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossBeamGeometry
{
    public static BossBeamVertex[] Build(
        BossBeamPlacement beam,
        Vector2i frameSize,
        BossBeamAnimationData animation,
        float referenceWidth,
        IReadOnlyList<Box2>? allowedAreas = null)
    {
        var delta = beam.End - beam.Start;
        var length = delta.Length();
        if (!float.IsFinite(length) || length <= 0 || !float.IsFinite(beam.Width) || beam.Width <= 0 ||
            !float.IsFinite(referenceWidth) || referenceWidth <= 0 || frameSize.X <= 0 || frameSize.Y <= 0 ||
            !float.IsFinite(animation.PixelsPerTile) || animation.PixelsPerTile <= 0 ||
            !float.IsFinite(animation.CoreLengthPixels) || animation.CoreLengthPixels <= 0 ||
            !float.IsFinite(animation.AnchorPixels.X) || !float.IsFinite(animation.AnchorPixels.Y))
            return [];

        var direction = delta / length;
        var perpendicular = new Vector2(-direction.Y, direction.X);
        var lengthScale = length * animation.PixelsPerTile / animation.CoreLengthPixels;
        var widthScale = beam.Width / referenceWidth;
        if (!float.IsFinite(lengthScale) || !float.IsFinite(widthScale))
            return [];

        BossBeamVertex Corner(float x, float y)
        {
            var point = beam.Start +
                direction * ((x - animation.AnchorPixels.X) / animation.PixelsPerTile * lengthScale) +
                perpendicular * ((animation.AnchorPixels.Y - y) / animation.PixelsPerTile * widthScale);
            return new(point, new Vector2(x / frameSize.X, y / frameSize.Y));
        }

        var quad = new List<BossBeamVertex>
        {
            Corner(0, 0), Corner(frameSize.X, 0), Corner(frameSize.X, frameSize.Y), Corner(0, frameSize.Y)
        };
        var triangles = new List<BossBeamVertex>();
        if (allowedAreas == null)
            Triangulate(quad, triangles);
        else
            foreach (var area in allowedAreas)
            {
                if (area.Width <= 0 || area.Height <= 0)
                    continue;

                var polygon = Clip(quad, vertex => vertex.Position.X - area.Left);
                polygon = Clip(polygon, vertex => area.Right - vertex.Position.X);
                polygon = Clip(polygon, vertex => vertex.Position.Y - area.Bottom);
                polygon = Clip(polygon, vertex => area.Top - vertex.Position.Y);
                Triangulate(polygon, triangles);
            }

        return triangles.ToArray();
    }

    private static List<BossBeamVertex> Clip(List<BossBeamVertex> polygon, Func<BossBeamVertex, float> distance)
    {
        var output = new List<BossBeamVertex>();
        if (polygon.Count == 0)
            return output;

        var previous = polygon[^1];
        var previousDistance = distance(previous);
        foreach (var current in polygon)
        {
            var currentDistance = distance(current);
            if ((currentDistance >= 0) != (previousDistance >= 0))
            {
                var fraction = previousDistance / (previousDistance - currentDistance);
                output.Add(new(
                    Vector2.Lerp(previous.Position, current.Position, fraction),
                    Vector2.Lerp(previous.TextureCoordinate, current.TextureCoordinate, fraction)));
            }

            if (currentDistance >= 0)
                output.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }

        return output;
    }

    private static void Triangulate(List<BossBeamVertex> polygon, List<BossBeamVertex> output)
    {
        for (var index = 1; index + 1 < polygon.Count; index++)
        {
            var a = polygon[index].Position - polygon[0].Position;
            var b = polygon[index + 1].Position - polygon[0].Position;
            if (MathF.Abs(a.X * b.Y - a.Y * b.X) <= 0.000001f)
                continue;
            output.Add(polygon[0]);
            output.Add(polygon[index]);
            output.Add(polygon[index + 1]);
        }
    }
}
