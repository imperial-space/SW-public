using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Boss;

public static class BossSpriteCanvas
{
    public static Box2 ExpandBounds(Box2 contentBounds, Vector2i contentSize, Vector2i canvasSize)
    {
        if (contentSize.X <= 0 || contentSize.Y <= 0 || canvasSize.X < contentSize.X || canvasSize.Y < contentSize.Y)
            return contentBounds;

        var leftPadding = (canvasSize.X - contentSize.X) / 2;
        var topPadding = (canvasSize.Y - contentSize.Y) / 2;
        var bottomPadding = canvasSize.Y - contentSize.Y - topPadding;
        var pixelSize = new Vector2(contentBounds.Width / contentSize.X, contentBounds.Height / contentSize.Y);
        return Box2.FromDimensions(
            new Vector2(contentBounds.Left - leftPadding * pixelSize.X, contentBounds.Bottom - bottomPadding * pixelSize.Y),
            new Vector2(canvasSize.X * pixelSize.X, canvasSize.Y * pixelSize.Y));
    }
}
