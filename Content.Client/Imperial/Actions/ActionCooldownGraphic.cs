using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Actions;


/// <summary>
/// Cooldown of an action button: the whole button is darkened and the dark part is swept away clockwise,
/// the seconds left are written in the middle, and when the action is ready the icon makes a small jump with a soft flash
/// </summary>
public sealed class ActionCooldownGraphic : Control
{
    /// <summary>
    /// Cooldowns shorter than this are not announced with the ready animation
    /// </summary>
    private const float MinPopCooldown = 3f;

    /// <summary>
    /// The ready animation plays only if the cooldown really ended just now, not while the button was not updated
    /// </summary>
    private const float MaxPopDelay = 0.25f;

    private const float PopTime = 0.4f;
    private const float SegmentDegrees = 1.5f;

    // Angle of the 12 o'clock direction on the screen, where the sweep starts
    private const float TopAngle = -MathF.PI / 2f;

    /// <summary>
    /// Below this many seconds left the time is shown with tenths
    /// </summary>
    private const float TenthsBelow = 10f;

    private const int GlyphWidth = 3;
    private const int GlyphHeight = 5;

    // The decimal point is one cell wide and sits on the bottom row
    private static readonly int[] Point = { 0b0, 0b0, 0b0, 0b0, 0b1 };

    // 3x5 pixel digits, one row per number (the lowest 3 bits), top to bottom
    private static readonly int[][] Digits =
    {
        new[] { 0b111, 0b101, 0b101, 0b101, 0b111 }, // 0
        new[] { 0b010, 0b110, 0b010, 0b010, 0b111 }, // 1
        new[] { 0b111, 0b001, 0b111, 0b100, 0b111 }, // 2
        new[] { 0b111, 0b001, 0b111, 0b001, 0b111 }, // 3
        new[] { 0b101, 0b101, 0b111, 0b001, 0b001 }, // 4
        new[] { 0b111, 0b100, 0b111, 0b001, 0b111 }, // 5
        new[] { 0b111, 0b100, 0b111, 0b101, 0b111 }, // 6
        new[] { 0b111, 0b001, 0b001, 0b010, 0b010 }, // 7
        new[] { 0b111, 0b101, 0b111, 0b101, 0b111 }, // 8
        new[] { 0b111, 0b101, 0b111, 0b001, 0b111 }, // 9
    };

    private static readonly Color Shade = new(0.03f, 0.03f, 0.07f, 0.74f);
    private static readonly Color EdgeCore = new(1f, 0.93f, 0.85f, 0.95f);
    private static readonly Color EdgeGlow = new(1f, 0.55f, 0.35f, 0.35f);
    private static readonly Color Warm = new(1f, 0.82f, 0.45f, 1f);

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _end;
    private bool _active;
    private TimeSpan? _popStart;
    private Texture? _icon;
    private Color _iconColor = Color.White;
    private Vector2[] _triangles = Array.Empty<Vector2>();
    private int _shownSeconds = -1;
    private string _secondsText = string.Empty;

    public ActionCooldownGraphic()
    {
        IoCManager.InjectDependencies(this);

        Visible = false;
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    /// Called every frame with the current cooldown of the action, null means there is none
    /// </summary>
    public void UpdateCooldown(TimeSpan? start, TimeSpan? end, Texture? icon, Color iconColor)
    {
        _icon = icon;
        _iconColor = iconColor;

        var now = _timing.CurTime;

        if (start != null && end != null && end.Value > now)
        {
            _start = start.Value;
            _end = end.Value;
            _active = true;
            _popStart = null;
        }
        else if (_active)
        {
            _active = false;

            if ((_end - _start).TotalSeconds >= MinPopCooldown && (now - _end).TotalSeconds < MaxPopDelay)
                _popStart = now;
        }

        var popping = _popStart != null && (now - _popStart.Value).TotalSeconds < PopTime;

        if (!popping) _popStart = null;

        Visible = _active || popping;
    }

    /// <summary>
    /// The button shows another action now, forget everything
    /// </summary>
    public void Reset()
    {
        _active = false;
        _popStart = null;
        _shownSeconds = -1;
        Visible = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);

        if (_active)
            DrawSweep(handle, size);
        else if (_popStart != null)
            DrawReady(handle, size);
    }

    private void DrawSweep(DrawingHandleScreen handle, Vector2 size)
    {
        var total = (float) (_end - _start).TotalSeconds;
        var left = (float) (_end - _timing.CurTime).TotalSeconds;

        if (total <= 0f || left <= 0f) return;

        var center = size / 2f;
        var half = center - new Vector2(2f * UIScale, 2f * UIScale);

        // The button is not laid out yet
        if (half.X <= 0f || half.Y <= 0f) return;

        // The sweep line goes clockwise from the top while the cooldown runs out
        var passed = 1f - Math.Clamp(left / total, 0f, 1f);
        var startAngle = TopAngle + MathF.Tau * passed;

        DrawShade(handle, center, half, startAngle);
        DrawSweepLine(handle, center, half, startAngle);
        DrawSeconds(handle, center, left);
    }

    /// <summary>
    /// Darkens the part of the button that is still to wait: from the sweep line clockwise up to the top
    /// </summary>
    private void DrawShade(DrawingHandleScreen handle, Vector2 center, Vector2 half, float startAngle)
    {
        var endAngle = TopAngle + MathF.Tau;
        var step = SegmentDegrees * MathF.PI / 180f;
        var segments = Math.Max(1, (int) MathF.Ceiling((endAngle - startAngle) / step));
        var vertices = segments * 3;

        if (_triangles.Length < vertices) _triangles = new Vector2[vertices];

        var previous = EdgePoint(center, half, startAngle);

        for (var i = 0; i < segments; i++)
        {
            var next = EdgePoint(center, half, startAngle + (endAngle - startAngle) * (i + 1) / segments);

            _triangles[i * 3] = center;
            _triangles[i * 3 + 1] = previous;
            _triangles[i * 3 + 2] = next;

            previous = next;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _triangles.AsSpan(0, vertices), Shade);
    }

    private static void DrawSweepLine(DrawingHandleScreen handle, Vector2 center, Vector2 half, float startAngle)
    {
        var edge = EdgePoint(center, half, startAngle);
        var normal = Vector2.Normalize(new Vector2(center.Y - edge.Y, edge.X - center.X));

        handle.DrawLine(center + normal, edge + normal, EdgeGlow);
        handle.DrawLine(center - normal, edge - normal, EdgeGlow);
        handle.DrawLine(center, edge, EdgeCore);
    }

    private void DrawSeconds(DrawingHandleScreen handle, Vector2 center, float left)
    {
        // Round up, so the number never shows zero while the ability is still on cooldown
        var tenths = (int) MathF.Ceiling(left * 10f);
        var key = tenths < TenthsBelow * 10f ? tenths : 1000 + (int) MathF.Ceiling(left);

        // The text changes at most ten times a second, no need to build a new string every frame
        if (key != _shownSeconds)
        {
            _shownSeconds = key;
            _secondsText = tenths < TenthsBelow * 10f
                ? $"{tenths / 10}.{tenths % 10}"
                : ((int) MathF.Ceiling(left)).ToString();
        }

        DrawPixelNumber(handle, center, _secondsText);
    }

    /// <summary>
    /// Writes the number with a 3x5 pixel font: every lit cell is a square of whole pixels, with a dark outline
    /// </summary>
    private void DrawPixelNumber(DrawingHandleScreen handle, Vector2 center, string text)
    {
        var cell = MathF.Max(2f, MathF.Round(3f * UIScale));
        var outline = MathF.Max(1f, MathF.Round(cell / 2f));
        var width = TextColumns(text) * cell;
        var height = GlyphHeight * cell;
        var left = MathF.Round(center.X - width / 2f);
        var top = MathF.Round(center.Y - height / 2f);
        var outlineColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);

        // Outline first so it never covers the neighbour digit
        for (var pass = 0; pass < 2; pass++)
        {
            var offset = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var glyph = GetGlyph(text[i], out var glyphWidth);

                if (glyph == null) continue;

                for (var row = 0; row < GlyphHeight; row++)
                {
                    for (var column = 0; column < glyphWidth; column++)
                    {
                        if ((glyph[row] & (1 << (glyphWidth - 1 - column))) == 0) continue;

                        var x = left + (offset + column) * cell;
                        var y = top + row * cell;

                        if (pass == 0)
                        {
                            handle.DrawRect(new UIBox2(x - outline, y - outline, x + cell + outline, y + cell + outline), outlineColor);
                        }
                        else
                        {
                            handle.DrawRect(new UIBox2(x, y, x + cell, y + cell), Color.White);
                        }
                    }
                }

                offset += glyphWidth + 1;
            }
        }
    }

    private static int[]? GetGlyph(char symbol, out int width)
    {
        if (symbol == '.')
        {
            width = 1;
            return Point;
        }

        width = GlyphWidth;

        var digit = symbol - '0';

        return digit is >= 0 and <= 9 ? Digits[digit] : null;
    }

    /// <summary>
    /// Width of the text in cells, with a one cell gap between the symbols
    /// </summary>
    private static int TextColumns(string text)
    {
        var columns = 0;

        foreach (var symbol in text)
        {
            if (GetGlyph(symbol, out var glyphWidth) == null) continue;

            columns += glyphWidth + 1;
        }

        return Math.Max(0, columns - 1);
    }

    private void DrawReady(DrawingHandleScreen handle, Vector2 size)
    {
        if (_popStart is not { } popStart) return;

        var k = Math.Clamp((float) (_timing.CurTime - popStart).TotalSeconds / PopTime, 0f, 1f);
        var inverse = 1f - k;
        var center = size / 2f;

        // Whole button flashes
        var flash = 0.18f * inverse * inverse;
        handle.DrawRect(new UIBox2(0f, 0f, size.X, size.Y), new Color(1f, 0.95f, 0.85f, flash));

        // Icon jumps up and settles back
        if (_icon != null)
        {
            var wave = MathF.Sin(MathF.PI * k) * (1f - 0.4f * k);
            var scale = 1f + 0.12f * wave;
            var lift = -3f * UIScale * wave;
            var halfSize = size * scale / 2f;
            var middle = center + new Vector2(0f, lift);

            handle.DrawTextureRect(
                _icon,
                new UIBox2(middle.X - halfSize.X, middle.Y - halfSize.Y, middle.X + halfSize.X, middle.Y + halfSize.Y),
                _iconColor);
        }

        // One faint ring spreading from the button edge
        var spread = (3f + 7f * k) * UIScale;

        handle.DrawRect(
            new UIBox2(-spread, -spread, size.X + spread, size.Y + spread),
            Warm.WithAlpha(0.4f * inverse),
            false);
    }

    /// <summary>
    /// Point where the ray from the center at the angle leaves the box with the given half size
    /// </summary>
    private static Vector2 EdgePoint(Vector2 center, Vector2 half, float angle)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var tx = MathF.Abs(direction.X) < 0.0001f ? float.MaxValue : half.X / MathF.Abs(direction.X);
        var ty = MathF.Abs(direction.Y) < 0.0001f ? float.MaxValue : half.Y / MathF.Abs(direction.Y);

        return center + direction * MathF.Min(tx, ty);
    }
}
