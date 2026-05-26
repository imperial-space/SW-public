using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

public sealed class AchievementTreeLayout : LayoutContainer
{
    private const float HalfCenter = 0.5f;
    private const float EdgeWidth  = 1f;

    private static readonly Color UnlockedEdge   = Color.FromHex("#5c8a3ecc");
    private static readonly Color UnlockedCenter = Color.FromHex("#2e5020cc");
    private static readonly Color LockedEdge     = Color.FromHex("#5a4428aa");
    private static readonly Color LockedCenter   = Color.FromHex("#2a1e0eaa");

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var child in Children)
        {
            if (child is AchievementTreeNode node)
                DrawEdges(handle, node);
        }
    }

    private void DrawEdges(DrawingHandleScreen handle, AchievementTreeNode node)
    {
        var requiredIds = AchievementTreeMenuWindow.GetRequiredIds(node.Proto);
        if (requiredIds.Count == 0)
            return;

        var from = NodeCenter(node);

        var bendOffset = node.PixelWidth * 1.5f;

        foreach (var child in Children)
        {
            if (child is not AchievementTreeNode parent || !requiredIds.Contains(parent.Proto.ID))
                continue;

            var (edge, center) = parent.Unlocked
                ? (UnlockedEdge, UnlockedCenter)
                : (LockedEdge,   LockedCenter);

            DrawConnector(handle, from, NodeCenter(parent), edge, center, bendOffset);
        }
    }

    private static void DrawConnector(DrawingHandleScreen handle,
    Vector2 from, Vector2 to, Color edge, Color center, float bendOffset)
    {
        var midX = from.X - bendOffset;

        if (from.X < to.X)
            midX = from.X + bendOffset;

        var topY    = MathF.Min(from.Y, to.Y);
        var bottomY = MathF.Max(from.Y, to.Y);

        DrawVerticalStrip(handle,   midX, topY, bottomY, edge, center);
        DrawHorizontalStrip(handle, from.X, midX, from.Y, edge, center);
        DrawHorizontalStrip(handle, midX,   to.X, to.Y,   edge, center);
    }

    private static void DrawHorizontalStrip(DrawingHandleScreen handle,
        float xA, float xB, float y, Color edge, Color center)
    {
        var left  = MathF.Min(xA, xB);
        var right = MathF.Max(xA, xB);

        var halfFull = HalfCenter + EdgeWidth;
        left  -= halfFull;
        right += halfFull;

        handle.DrawRect(new UIBox2(left, y - halfFull,   right, y - HalfCenter), edge);
        handle.DrawRect(new UIBox2(left, y - HalfCenter, right, y + HalfCenter), center);
        handle.DrawRect(new UIBox2(left, y + HalfCenter, right, y + halfFull),   edge);
    }

    private static void DrawVerticalStrip(DrawingHandleScreen handle,
        float x, float top, float bottom, Color edge, Color center)
    {
        var halfFull = HalfCenter + EdgeWidth;

        handle.DrawRect(new UIBox2(x - halfFull,   top, x - HalfCenter, bottom), edge);
        handle.DrawRect(new UIBox2(x - HalfCenter, top, x + HalfCenter, bottom), center);
        handle.DrawRect(new UIBox2(x + HalfCenter, top, x + halfFull,   bottom), edge);
    }

    private static Vector2 NodeCenter(AchievementTreeNode node)
        => node.PixelPosition + new Vector2(node.PixelWidth / 2f, node.PixelHeight / 2f);
}
