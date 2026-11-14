using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss.HealthBar;

public sealed class BossHealthBarUI : BoxContainer
{
    private const float BarWidth = 500f;
    private const float BarHeight = 22f;
    private const float GhostLerpSpeed = 0.25f;

    private const float BarTextureScale = 2f;
    private const float DefaultFramePatchMargin = 4f;
    private const float DefaultFillPatchMargin = 3f;
    private const float DefaultFillHorizontalInset = 2f;
    private const float DefaultFillTopInset = 4f;
    private const float DefaultFillBottomInset = 3f;

    private const string BarFrameTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_frame.png";
    private const string BarFillTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_fill.png";

    private static readonly Color BarBackground = Color.FromHex("#1A1414");
    private static readonly Color HealthColor = Color.FromHex("#E5483F");
    private static readonly Color GhostColor = Color.FromHex("#D18C8C");

    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly Label _bossNameLabel;
    private readonly BossFramedStatBar _bar;

    private float _filledRatio = 1f;
    private float _ghostRatio = 1f;

    private float _displayScale = 1;

    public void SetDisplayScale(float scale)
    {
        if (MathF.Abs(_displayScale - scale) < 0.001f) return;
        _displayScale = scale;
        SetWidth = MinWidth = BarWidth * scale;
        _bar.SetWidth = _bar.MinWidth = BarWidth * scale;
        _bar.SetHeight = _bar.MinHeight = BarHeight * scale;
        _bar.MinSize = new Vector2(BarWidth, BarHeight) * scale;
        _bossNameLabel.Margin = new Thickness(0, 0, 0, 4 * scale);
        if (_resourceCache.TryGetResource<FontResource>(new ResPath("/Fonts/Imperial/Vinque/Vinque.otf"), out var font))
            _bossNameLabel.FontOverride = new VectorFont(font, Math.Max(1, (int)MathF.Round(16 * scale)));
    }

    public BossHealthBarUI()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalAlignment = HAlignment.Center;
        SetWidth = BarWidth;
        MinWidth = BarWidth;

        _bossNameLabel = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            FontColorOverride = Color.White
        };

        if (_resourceCache.TryGetResource<FontResource>(new ResPath("/Fonts/Imperial/Vinque/Vinque.otf"), out var fontRes))
        {
            _bossNameLabel.FontOverride = new VectorFont(fontRes, 16);
        }

        _bar = new BossFramedStatBar(_resourceCache)
        {
            SetHeight = BarHeight,
            MinHeight = BarHeight,
            SetWidth = BarWidth,
            MinWidth = BarWidth,
        };

        AddChild(_bossNameLabel);
        AddChild(_bar);
    }

    public void SetData(float targetRatio, float deltaTime, string? bossName, Color? barColor = null)
    {
        targetRatio = Math.Clamp(targetRatio, 0f, 1f);

        if (targetRatio > _filledRatio)
            _ghostRatio = targetRatio;

        _filledRatio = targetRatio;

        if (_ghostRatio > _filledRatio)
            _ghostRatio = MathF.Max(_filledRatio, _ghostRatio - deltaTime * GhostLerpSpeed);

        _bossNameLabel.Text = bossName ?? string.Empty;

        if (barColor.HasValue)
            _bar.SetColor(barColor.Value);

        _bar.SetValues(_filledRatio, _ghostRatio);
    }

    private sealed class BossFramedStatBar : LayoutContainer
    {
        private readonly LayoutContainer _fillRegion;
        private readonly PanelContainer _ghostSprite;
        private readonly PanelContainer _fillSprite;
        private float _fillValue;
        private float _ghostValue;
        private StyleBoxTexture _fillStyleBox = new();

        public BossFramedStatBar(IResourceCache resourceCache)
        {
            MouseFilter = MouseFilterMode.Ignore;
            MinSize = new Vector2(BarWidth, BarHeight);
            HorizontalExpand = true;

            var fillTexture = resourceCache.GetResource<TextureResource>(BarFillTexturePath).Texture;
            var frameTexture = resourceCache.GetResource<TextureResource>(BarFrameTexturePath).Texture;

            var backgroundStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = BarBackground,
                Texture = fillTexture,
            };
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFillPatchMargin);
            backgroundStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFillPatchMargin);

            var ghostStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = GhostColor,
                Texture = fillTexture,
            };
            ghostStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFillPatchMargin);
            ghostStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFillPatchMargin);
            ghostStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFillPatchMargin);
            ghostStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFillPatchMargin);

            _fillStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = HealthColor,
                Texture = fillTexture,
            };
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFillPatchMargin);
            _fillStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFillPatchMargin);

            var frameStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Texture = frameTexture,
            };
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Left, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Top, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Right, DefaultFramePatchMargin);
            frameStyleBox.SetPatchMargin(StyleBox.Margin.Bottom, DefaultFramePatchMargin);

            _fillRegion = new LayoutContainer
            {
                MouseFilter = MouseFilterMode.Ignore,
                RectClipContent = true,
                InheritChildMeasure = false,
            };
            SetAnchorPreset(_fillRegion, LayoutPreset.Wide);
            SetMarginLeft(_fillRegion, DefaultFillHorizontalInset);
            SetMarginTop(_fillRegion, DefaultFillTopInset);
            SetMarginRight(_fillRegion, -DefaultFillHorizontalInset);
            SetMarginBottom(_fillRegion, -DefaultFillBottomInset);

            var background = new PanelContainer
            {
                PanelOverride = backgroundStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
            };
            SetAnchorPreset(background, LayoutPreset.Wide);

            _ghostSprite = new PanelContainer
            {
                PanelOverride = ghostStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false,
            };
            SetAnchorPreset(_ghostSprite, LayoutPreset.LeftWide);

            _fillSprite = new PanelContainer
            {
                PanelOverride = _fillStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false,
            };
            SetAnchorPreset(_fillSprite, LayoutPreset.LeftWide);

            var frame = new PanelContainer
            {
                PanelOverride = frameStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
            };
            SetAnchorPreset(frame, LayoutPreset.Wide);

            AddChild(_fillRegion);
            AddChild(frame);
            _fillRegion.AddChild(background);
            _fillRegion.AddChild(_ghostSprite);
            _fillRegion.AddChild(_fillSprite);

            OnResized += UpdateFills;
            _fillRegion.OnResized += UpdateFills;
        }

        public void SetValues(float fill, float ghost)
        {
            _fillValue = Math.Clamp(fill, 0f, 1f);
            _ghostValue = Math.Clamp(ghost, 0f, 1f);
            UpdateFills();
        }

        private void UpdateFills()
        {
            var maxWidth = MathF.Round(_fillRegion.Size.X * UIScale) / UIScale;

            var ghostWidth = Math.Clamp(MathF.Ceiling(_fillRegion.Size.X * _ghostValue * UIScale) / UIScale, 0f, maxWidth);
            _ghostSprite.Visible = ghostWidth > 0.5f;
            SetMarginRight(_ghostSprite, ghostWidth);

            var fillWidth = Math.Clamp(MathF.Ceiling(_fillRegion.Size.X * _fillValue * UIScale) / UIScale, 0f, maxWidth);
            _fillSprite.Visible = fillWidth > 0.5f;
            SetMarginRight(_fillSprite, fillWidth);
        }

        public void SetColor(Color color)
        {
            _fillStyleBox.Modulate = color;
        }
    }
}
