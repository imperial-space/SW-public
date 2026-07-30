using System;
using System.Numerics;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Inventory;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.UserInterface.Elements;

public sealed class ArmorHotbarVisualControl : BoxContainer
{
    private const float BarSmoothingSpeed = 14f;
    private const float BarTextureScale = 2f;
    private const float DefaultFramePatchMargin = 4f;
    private const float DefaultFillPatchMargin = 3f;
    private const float DefaultFillHorizontalInset = 2f;
    private const float DefaultFillTopInset = 4f;
    private const float DefaultFillBottomInset = 3f;
    private const float VerticalBarWidth = 20f;

    private const string VerticalBarFrameTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_frame.png";
    private const string VerticalBarFillTexturePath = "/Textures/Imperial/Medieval/Interface/StatusBars/vitals_fill.png";

    private static readonly Color BarBackground = Color.FromHex("#1A1414");
    private static readonly Color ArmorColor = Color.FromHex("#ece3f1");

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly VerticalFramedStatBar _armorBar;

    private EntityUid? _trackedEntity;
    private float? _displayedArmorRatio;

    public ArmorHotbarVisualControl()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Ignore;
        VerticalExpand = true;

        _armorBar = new VerticalFramedStatBar(_resourceCache)
        {
            FillColor = ArmorColor,
            Visible = false
        };

        AddChild(_armorBar);
        ResetTrackedEntity(null);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateArmor(args.DeltaSeconds);
    }

    private void UpdateArmor(float frameTime)
    {
        if (_player.LocalEntity is not { } entity || !_entityManager.EntityExists(entity))
        {
            _armorBar.Visible = false;
            ResetTrackedEntity(null);
            return;
        }

        if (_trackedEntity != entity)
            ResetTrackedEntity(entity);

        float? armorPercentage = GetArmorDurabilityPercentage(entity);

        if (armorPercentage is not { } percentage)
        {
            _armorBar.Visible = false;
            _displayedArmorRatio = null;
            return;
        }

        var ratio = Math.Clamp(percentage, 0f, 1f);

        if (_displayedArmorRatio == null)
            _displayedArmorRatio = ratio;
        else
        {
            var smoothing = 1f - MathF.Exp(-BarSmoothingSpeed * frameTime);
            _displayedArmorRatio = MathHelper.Lerp(_displayedArmorRatio.Value, ratio, smoothing);
        }

        _armorBar.Value = _displayedArmorRatio.Value;
        _armorBar.Visible = true;
    }

    private float? GetArmorDurabilityPercentage(EntityUid user)
    {
        if (!_entityManager.TryGetComponent<InventoryComponent>(user, out var inventory))
            return null;

        var currentArmorHp = 0f;
        var maxArmorHp = 0f;
        var hasArmor = false;

        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item))
        {
            if (!_entityManager.TryGetComponent<MedievalArmorIntegrityComponent>(item, out var armorIntegrity))
                continue;

            hasArmor = true;
            currentArmorHp += armorIntegrity.CurrentArmorHP;
            maxArmorHp += armorIntegrity.MaxArmorHP;
        }

        if (!hasArmor)
            return null;

        return maxArmorHp <= 0f ? 0f : Math.Clamp(currentArmorHp / maxArmorHp, 0f, 1f);
    }

    private void ResetTrackedEntity(EntityUid? entity)
    {
        _trackedEntity = entity;
        _displayedArmorRatio = null;
    }

    private sealed class VerticalFramedStatBar : LayoutContainer
    {
        private readonly LayoutContainer _fillRegion;
        private readonly PanelContainer _fillSprite;
        private readonly StyleBoxTexture _fillStyleBox;
        private float _value;

        public Color FillColor
        {
            get => _fillStyleBox.Modulate;
            set => _fillStyleBox.Modulate = value;
        }

        public float Value
        {
            get => _value;
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_value - clamped) < 0.001f)
                    return;

                _value = clamped;
                UpdateFill();
            }
        }

        public VerticalFramedStatBar(IResourceCache resourceCache)
        {
            MouseFilter = MouseFilterMode.Ignore;
            MinSize = new Vector2(VerticalBarWidth, 40f);
            VerticalExpand = true;
            VerticalAlignment = VAlignment.Stretch;
            SetWidth = VerticalBarWidth;

            var fillTexture = resourceCache.GetResource<TextureResource>(VerticalBarFillTexturePath).Texture;
            var frameTexture = resourceCache.GetResource<TextureResource>(VerticalBarFrameTexturePath).Texture;

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

            _fillStyleBox = new StyleBoxTexture
            {
                Mode = StyleBoxTexture.StretchMode.Tile,
                TextureScale = Vector2.One * BarTextureScale,
                Modulate = Color.White,
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

            _fillSprite = new PanelContainer
            {
                PanelOverride = _fillStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false,
            };
            SetAnchorPreset(_fillSprite, LayoutPreset.BottomWide);
            SetMarginLeft(_fillSprite, 0f);
            SetMarginRight(_fillSprite, 0f);
            SetMarginBottom(_fillSprite, 0f);

            var frame = new PanelContainer
            {
                PanelOverride = frameStyleBox,
                MouseFilter = MouseFilterMode.Ignore,
            };
            SetAnchorPreset(frame, LayoutPreset.Wide);

            AddChild(_fillRegion);
            AddChild(frame);
            _fillRegion.AddChild(background);
            _fillRegion.AddChild(_fillSprite);

            OnResized += UpdateFill;
            _fillRegion.OnResized += UpdateFill;
        }

        private void UpdateFill()
        {
            var maxHeight = MathF.Round(_fillRegion.Size.Y * UIScale) / UIScale;
            var height = Math.Clamp(MathF.Ceiling(_fillRegion.Size.Y * _value * UIScale) / UIScale, 0f, maxHeight);

            _fillSprite.Visible = height > 0.5f;
            SetMarginTop(_fillSprite, -height);
        }
    }
}
