using System.Numerics;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.Imperial.Medieval.CapturePoint;

public sealed class CapturePointOverlay : Overlay
{
    private readonly CapturePointSystem _system;
    private readonly IEntityManager _entManager;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const float BarWidth = 300f;
    private const float BarHeight = 22f;
    private const float BarTopMargin = 60f;
    private const float BarBorderWidth = 2f;
    private const float TextGap = 6f;

    private static readonly Color NeutralColor = new(0.5f, 0.5f, 0.5f, 0.9f);
    private static readonly Color BackgroundColor = new(0.15f, 0.15f, 0.15f, 0.85f);
    private static readonly Color BorderColor = new(0.8f, 0.8f, 0.8f, 0.5f);
    private static readonly Color TextColor = new(1f, 1f, 1f, 1f);
    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.8f);

    /// <summary>
    /// Color interpolation exponent
    /// </summary>
    private const float DominancePower = 2.5f;

    private readonly Font _font;
    private readonly Font _fontBold;
    private readonly Font _fontSmall;

    public CapturePointOverlay(CapturePointSystem system, IResourceCache cache, IEntityManager entManager)
    {
        _system = system;
        _entManager = entManager;
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
        _fontBold = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 14);
        _fontSmall = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var pointUid = _entManager.GetEntity(_system.OverlayPointEntity);
        if (!_entManager.TryGetComponent<CapturePointComponent>(pointUid, out var comp))
            return;

        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var screenSize = args.Viewport.Size;
        var centerX = screenSize.X / 2f;
        const float uiScale = 1f;

        var barX = centerX - BarWidth / 2f;

        var nameText = comp.PointName;
        var nameDim = handle.GetDimensions(_fontBold, nameText, uiScale);
        var namePos = new Vector2(centerX - nameDim.X / 2f, BarTopMargin - nameDim.Y - 6f);
        DrawTextWithShadow(handle, _fontBold, namePos, nameText, uiScale, TextColor);

        var borderRect = new UIBox2(
            barX - BarBorderWidth,
            BarTopMargin - BarBorderWidth,
            barX + BarWidth + BarBorderWidth,
            BarTopMargin + BarHeight + BarBorderWidth);
        handle.DrawRect(borderRect, BorderColor);

        var barRect = new UIBox2(barX, BarTopMargin, barX + BarWidth, BarTopMargin + BarHeight);
        handle.DrawRect(barRect, BackgroundColor);

        const float barCenterY = BarTopMargin + BarHeight / 2f;
        var nextY = BarTopMargin + BarHeight + TextGap;

        switch (comp.State)
        {
            case CapturePointState.Idle:
            {
                if (comp.OwningFaction != null)
                {
                    var ownerColor = WithAlpha(_system.GetFactionColor(comp.OwningFaction.Value), 0.9f);
                    var stripe = new UIBox2(barX, BarTopMargin + BarHeight - 3f, barX + BarWidth, BarTopMargin + BarHeight);
                    handle.DrawRect(stripe, ownerColor);
                }

                string ownerText;
                Color ownerTextColor;
                if (comp.OwningFaction != null)
                {
                    ownerText = Loc.GetString("medieval-capture-point-overlay-owner",
                        ("factionName", _system.GetFactionDisplayName(comp.OwningFaction.Value)));
                    ownerTextColor = _system.GetFactionColor(comp.OwningFaction.Value);
                }
                else
                {
                    ownerText = Loc.GetString("medieval-capture-point-overlay-neutral");
                    ownerTextColor = NeutralColor;
                }

                var ownerDim = handle.GetDimensions(_font, ownerText, uiScale);
                var ownerPos = new Vector2(centerX - ownerDim.X / 2f, barCenterY - ownerDim.Y / 2f);
                DrawTextWithShadow(handle, _font, ownerPos, ownerText, uiScale, ownerTextColor);
                break;
            }

            case CapturePointState.Capturing:
            {
                var color0 = comp.AllowedFactions.Count > 0
                    ? WithAlpha(_system.GetFactionColor(comp.AllowedFactions[0]), 0.9f)
                    : NeutralColor;
                var color1 = comp.AllowedFactions.Count > 1
                    ? WithAlpha(_system.GetFactionColor(comp.AllowedFactions[1]), 0.9f)
                    : NeutralColor;

                var count0 = comp.FactionCounts.Length > 0 ? comp.FactionCounts[0] : 0;
                var count1 = comp.FactionCounts.Length > 1 ? comp.FactionCounts[1] : 0;

                var dominance = 0f;
                var total = count0 + count1;
                if (total > 0)
                    dominance = (float)(count0 - count1) / total;

                Color fillColor;
                if (dominance >= 0)
                {
                    var t = MathF.Pow(dominance, 1f / DominancePower);
                    fillColor = LerpColor(NeutralColor, color0, t);
                }
                else
                {
                    var t = MathF.Pow(-dominance, 1f / DominancePower);
                    fillColor = LerpColor(NeutralColor, color1, t);
                }

                var progress = Math.Clamp(_system.GetCaptureProgress(), 0f, 1f);
                var fillWidth = BarWidth * progress;

                if (fillWidth > 0)
                {
                    var fillRect = new UIBox2(barX, BarTopMargin, barX + fillWidth, BarTopMargin + BarHeight);
                    handle.DrawRect(fillRect, fillColor);
                }

                var percentText = Loc.GetString("medieval-capture-point-overlay-percent",
                    ("value", (int)(progress * 100)));
                var percentDim = handle.GetDimensions(_fontBold, percentText, uiScale);
                var percentPos = new Vector2(centerX - percentDim.X / 2f, barCenterY - percentDim.Y / 2f);
                DrawTextWithShadow(handle, _fontBold, percentPos, percentText, uiScale, TextColor);

                var timeRemaining = _system.GetCaptureTimeRemaining();
                var minutes = (int)(timeRemaining / 60);
                var seconds = (int)(timeRemaining % 60);
                var timeText = Loc.GetString("medieval-capture-point-overlay-time",
                    ("minutes", minutes.ToString("D2")), ("seconds", seconds.ToString("D2")));

                var timeDim = handle.GetDimensions(_fontBold, timeText, uiScale);
                var timePos = new Vector2(centerX - timeDim.X / 2f, nextY);
                DrawTextWithShadow(handle, _fontBold, timePos, timeText, uiScale, TextColor);
                //nextY = timePos.Y + timeDim.Y + TextGap;
                break;
            }

            case CapturePointState.Cooldown:
            {
                handle.DrawRect(barRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

                var cdRemaining = _system.GetCooldownRemaining();
                var cdMin = (int)(cdRemaining / 60);
                var cdSec = (int)(cdRemaining % 60);
                var cdText = Loc.GetString("medieval-capture-point-overlay-cooldown",
                    ("minutes", cdMin.ToString("D2")), ("seconds", cdSec.ToString("D2")));

                var cdDim = handle.GetDimensions(_font, cdText, uiScale);
                var cdPos = new Vector2(centerX - cdDim.X / 2f, barCenterY - cdDim.Y / 2f);
                DrawTextWithShadow(handle, _font, cdPos, cdText, uiScale, NeutralColor);

                if (comp.OwningFaction != null)
                {
                    var ownerText = Loc.GetString("medieval-capture-point-overlay-owner",
                        ("factionName", _system.GetFactionDisplayName(comp.OwningFaction.Value)));
                    var ownerColor = _system.GetFactionColor(comp.OwningFaction.Value);
                    var ownerDim = handle.GetDimensions(_fontSmall, ownerText, uiScale);
                    var ownerPos = new Vector2(centerX - ownerDim.X / 2f, nextY);
                    DrawTextWithShadow(handle, _fontSmall, ownerPos, ownerText, uiScale, ownerColor);
                }
                break;
            }
            default:
                throw new InvalidOperationException($"Unexpected CapturePointState: {comp.State}");
        }
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.R, c.G, c.B, a);
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t);
    }

    private static void DrawTextWithShadow(DrawingHandleScreen handle, Font font, Vector2 pos, string text, float uiScale, Color color)
    {
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, uiScale, ShadowColor);
        handle.DrawString(font, pos, text, uiScale, color);
    }
}
