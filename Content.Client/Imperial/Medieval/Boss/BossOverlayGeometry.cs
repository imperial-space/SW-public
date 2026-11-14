using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Medieval.Boss;

public static class BossOverlayGeometry
{
    public static Box2[] MaskRows(IEnumerable<Vector2i> danger)
    {
        var rows = new List<Box2>();
        foreach (var row in danger.Distinct().GroupBy(tile => tile.Y))
        {
            var columns = row.Select(tile => tile.X).OrderBy(x => x).ToArray();
            if (columns.Length == 0) continue;
            var start = columns[0];
            var last = start;
            foreach (var column in columns.Skip(1))
            {
                if (column > last + 1)
                {
                    rows.Add(new Box2(start, row.Key, last + 1, row.Key + 1));
                    start = column;
                }
                last = column;
            }
            rows.Add(new Box2(start, row.Key, last + 1, row.Key + 1));
        }
        return rows.ToArray();
    }

    public static float MaximumRadius(IReadOnlyList<Box2> mask, Vector2i origin)
    {
        var center = new Vector2(origin.X + 0.5f, origin.Y + 0.5f);
        var maximum = 0f;
        foreach (var bounds in mask)
        {
            var x = MathF.Max(MathF.Abs(bounds.Left - center.X), MathF.Abs(bounds.Right - center.X));
            var y = MathF.Max(MathF.Abs(bounds.Bottom - center.Y), MathF.Abs(bounds.Top - center.Y));
            maximum = MathF.Max(maximum, MathF.Sqrt(x * x + y * y));
        }
        return maximum;
    }

    public static Box2 TextureBounds(Vector2i size, Vector2 anchor, float artRadius, float radius)
    {
        var factor = radius / artRadius;
        var bottom = new Vector2(-anchor.X, anchor.Y - size.Y) * factor;
        return Box2.FromDimensions(bottom, new Vector2(size.X, size.Y) * factor);
    }
}
