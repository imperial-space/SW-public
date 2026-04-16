using System.Numerics;
using Content.Shared.Fishing.Bui;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Fishing;

public sealed class FishingMinigameControl : PanelContainer
{
    private static readonly SpriteSpecifier.Rsi ScaleSprite = new(new ResPath("/Textures/Imperial/Fishing/UI/FishMinigame/fishing_scale.rsi"), "icon");
    private static readonly SpriteSpecifier.Rsi FloatSprite = new(new ResPath("/Textures/Imperial/Fishing/UI/FishMinigame/fishing_scale_float.rsi"), "icon");
    private static readonly SpriteSpecifier.Rsi FishSprite = new(new ResPath("/Textures/Imperial/Fishing/UI/FishMinigame/fishing_scale_fish.rsi"), "icon");

    private static readonly Vector2 WindowSize = new(498f, 192f);
    private static readonly Vector2 ScaleSize = new(402f, 87f);
    private static readonly Vector2 FloatSize = new(33f, 48f);
    private static readonly Vector2 FishSize = new(72f, 39f);
    private static readonly Vector2 ScaleOffset = new(48f, 48f);

    // Inner rectangle bounds on fishing_scale where indicators can move.
    private const float TrackLeft = 9f;
    private const float TrackRight = 393f;
    private const float FloatTrackCenterY = 18f;
    private const float FishTrackCenterY = 60f;

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private readonly TextureRect _floatIcon;
    private readonly TextureRect _fishIcon;
    private float _baseAsyncTimeStep = 1f;
    private float _tensionAcceleration;
    private float _tensionAccelerationDelta;
    private float _tensionAccelerationDeltaPressed;
    private float _progressPerTick;
    private bool _active;
    private bool _holdingLmb;
    private bool _resultSent;
    private float _tension;
    private float _progress;

    public event Action<FishingMinigameResult>? MinigameFinished;

    public FishingMinigameControl()
    {
        IoCManager.InjectDependencies(this);
        var sprite = _entityManager.System<SpriteSystem>();

        MouseFilter = MouseFilterMode.Ignore;
        MinSize = WindowSize;
        SetSize = WindowSize;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1E2128EE"),
            BorderColor = Color.FromHex("#6D7386"),
            BorderThickness = new Thickness(2f),
        };

        LayoutContainer.SetAnchorLeft(this, 0.5f);
        LayoutContainer.SetAnchorRight(this, 0.5f);
        LayoutContainer.SetAnchorTop(this, 1f);
        LayoutContainer.SetAnchorBottom(this, 1f);
        LayoutContainer.SetMarginLeft(this, -WindowSize.X / 2f);
        LayoutContainer.SetMarginRight(this, WindowSize.X / 2f);
        LayoutContainer.SetMarginTop(this, -288f);
        LayoutContainer.SetMarginBottom(this, -96f);

        var layout = new LayoutContainer();
        AddChild(layout);
        LayoutContainer.SetAnchorPreset(layout, LayoutContainer.LayoutPreset.Wide);

        var scale = new TextureRect
        {
            Texture = sprite.Frame0(ScaleSprite),
            SetSize = ScaleSize,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        layout.AddChild(scale);
        LayoutContainer.SetAnchorPreset(scale, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(scale, ScaleOffset.X);
        LayoutContainer.SetMarginRight(scale, ScaleOffset.X + ScaleSize.X);
        LayoutContainer.SetMarginTop(scale, ScaleOffset.Y);
        LayoutContainer.SetMarginBottom(scale, ScaleOffset.Y + ScaleSize.Y);

        _floatIcon = new TextureRect
        {
            Texture = sprite.Frame0(FloatSprite),
            SetSize = FloatSize,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        layout.AddChild(_floatIcon);
        LayoutContainer.SetAnchorPreset(_floatIcon, LayoutContainer.LayoutPreset.TopLeft);

        _fishIcon = new TextureRect
        {
            Texture = sprite.Frame0(FishSprite),
            SetSize = FishSize,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        layout.AddChild(_fishIcon);
        LayoutContainer.SetAnchorPreset(_fishIcon, LayoutContainer.LayoutPreset.TopLeft);

        SetValues(50f, 0f);
    }

    public void StartMinigame(FishingMinigameBoundUserInterfaceState state)
    {
        _baseAsyncTimeStep = Math.Max(0.001f, state.BaseAsyncTimeStep);
        _tensionAccelerationDelta = state.TensionAccelerationDelta;
        _tensionAccelerationDeltaPressed = state.TensionAccelerationDeltaPressed;
        _progressPerTick = state.ProgressPerTick;
        _tension = state.Tension;
        _progress = state.Progress;
        _tensionAcceleration = state.TensionAcceleration;
        _active = true;
        _resultSent = false;

        SetValues(_tension, _progress);
    }

    public void StopMinigameFromServer()
    {
        _active = false;
        _resultSent = true;
    }

    public void SetValues(float tension, float progress)
    {
        var clampedTension = Math.Clamp(tension, 0f, 100f);
        var clampedProgress = Math.Clamp(progress, 0f, 100f);

        // Float tracks tension in the upper rectangle, fish tracks progress in the lower one.
        SetIndicatorPosition(_floatIcon, clampedTension, FloatTrackCenterY);
        SetIndicatorPosition(_fishIcon, clampedProgress, FishTrackCenterY);
    }

    private void SetIndicatorPosition(TextureRect icon, float value, float trackCenterY)
    {
        var leftBound = ScaleOffset.X + TrackLeft;
        var rightBound = ScaleOffset.X + TrackRight;
        var iconWidth = icon.SetSize.X;
        var iconHeight = icon.SetSize.Y;

        var ratio = value / 100f;
        var x = leftBound + (rightBound - leftBound - iconWidth) * ratio;
        var y = ScaleOffset.Y + trackCenterY - iconHeight / 2f;

        LayoutContainer.SetMarginLeft(icon, x);
        LayoutContainer.SetMarginRight(icon, x + iconWidth);
        LayoutContainer.SetMarginTop(icon, y);
        LayoutContainer.SetMarginBottom(icon, y + iconHeight);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateHoldingState();

        if (!_active || _resultSent)
            return;

        var scale = args.DeltaSeconds / _baseAsyncTimeStep;
        if (scale <= 0f)
            return;

        var accelerationDelta = _holdingLmb
            ? _tensionAccelerationDeltaPressed
            : _tensionAccelerationDelta;

        _tensionAcceleration += accelerationDelta * scale;
        _tension += _tensionAcceleration * scale;

        if (_holdingLmb)
            _progress += _progressPerTick * scale;

        SetValues(_tension, _progress);

        if (_tension is <= 0f or >= 100f)
        {
            FinishMinigame(FishingMinigameResult.Exit);
            return;
        }

        if (_progress > 100f)
            FinishMinigame(FishingMinigameResult.Complete);
    }

    private void UpdateHoldingState()
    {
        var holding = false;
        foreach (var function in _input.DownKeyFunctions)
        {
            if (function != EngineKeyFunctions.Use)
                continue;

            holding = true;
            break;
        }

        if (holding == _holdingLmb)
            return;

        _holdingLmb = holding;
    }

    private void FinishMinigame(FishingMinigameResult result)
    {
        _active = false;
        _resultSent = true;
        MinigameFinished?.Invoke(result);
    }
}
