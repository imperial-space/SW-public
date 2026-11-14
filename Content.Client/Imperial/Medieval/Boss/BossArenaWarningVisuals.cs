using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;
using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Client.Imperial.Medieval.Boss;

public static class BossArenaWarningVisuals
{

    public static Angle ProfileRotation(Angle onScreen)
        => SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, onScreen.Reduced().FlipPositive()) switch
        {
            RsiDirection.East => Angle.FromDegrees(90),
            RsiDirection.North => Angle.FromDegrees(180),
            RsiDirection.West => Angle.FromDegrees(270),
            _ => Angle.Zero,
        };

    public const float OutlineWidth = 2f / 32;

    public static IEnumerable<Box2> Outline(IReadOnlyCollection<Vector2i> tiles)
    {
        var area = tiles.ToHashSet();
        foreach (var tile in area)
        {
            var bottom = !area.Contains(tile + new Vector2i(0, -1));
            var top = !area.Contains(tile + new Vector2i(0, 1));
            if (bottom)
                yield return Box2.FromDimensions(new Vector2(tile.X, tile.Y), new Vector2(1, OutlineWidth));
            if (top)
                yield return Box2.FromDimensions(new Vector2(tile.X, tile.Y + 1 - OutlineWidth), new Vector2(1, OutlineWidth));

            var start = tile.Y + (bottom ? OutlineWidth : 0);
            var height = 1 - (bottom ? OutlineWidth : 0) - (top ? OutlineWidth : 0);
            if (!area.Contains(tile + new Vector2i(-1, 0)))
                yield return Box2.FromDimensions(new Vector2(tile.X, start), new Vector2(OutlineWidth, height));
            if (!area.Contains(tile + new Vector2i(1, 0)))
                yield return Box2.FromDimensions(new Vector2(tile.X + 1 - OutlineWidth, start), new Vector2(OutlineWidth, height));
        }
    }

    public static bool SpansArena(BossTelegraphPattern pattern)
        => pattern is BossTelegraphPattern.Sinistra or BossTelegraphPattern.Dextra or
            BossTelegraphPattern.Frons or BossTelegraphPattern.Tergum or
            BossTelegraphPattern.Rejection or BossTelegraphPattern.Invitation or
            BossTelegraphPattern.Cross or BossTelegraphPattern.Divide or
            BossTelegraphPattern.CardinalCone or BossTelegraphPattern.Stack ||
            BossTilePatterns.IsConstellation(pattern) || BossTilePatterns.IsBlackout(pattern);

    public static IEnumerable<Vector2i> Tiles(BossTelegraphPattern pattern, IReadOnlyCollection<Vector2i> danger,
        int halfSize, int overflow, Vector2i origin = default, int facing = 0)
    {
        foreach (var tile in danger)
            yield return tile;
        if (overflow <= 0 || halfSize < 0 || !SpansArena(pattern))
            yield break;

        var border = danger.ToHashSet();
        var padding = Math.Min(overflow, 1);

        var cone = pattern == BossTelegraphPattern.CardinalCone
            ? BossTilePatterns.Danger(pattern, [], facing: facing, origin: origin, halfSize: halfSize + padding).ToHashSet()
            : null;
        for (var y = -halfSize - padding; y <= halfSize + padding; y++)
            for (var x = -halfSize - padding; x <= halfSize + padding; x++)
            {
                if (Math.Abs(x) <= halfSize && Math.Abs(y) <= halfSize)
                    continue;
                var edge = new Vector2i(Math.Clamp(x, -halfSize, halfSize), Math.Clamp(y, -halfSize, halfSize));
                if (border.Contains(edge) && (cone == null || cone.Contains(new Vector2i(x, y))))
                    yield return new Vector2i(x, y);
            }
    }

    public static float LineLength(Vector2 origin, Vector2 direction, float originalLength, int halfSize, int overflow)
    => overflow <= 0 ? originalLength : MathF.Max(originalLength,
        BossExecutionLineVisuals.LengthToEdge(origin, direction, halfSize + 1) - BossExecutionLineVisuals.EdgeExtension);
}
