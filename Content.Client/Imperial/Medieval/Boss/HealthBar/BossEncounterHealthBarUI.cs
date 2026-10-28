using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Client.Imperial.Medieval.Boss.HealthBar;

public sealed class BossEncounterHealthBarUI : BoxContainer
{
    [Dependency] private readonly IResourceCache _resources = default!;
    private readonly Label _name;
    private readonly ProgressBar _health;
    private readonly BoxContainer _casting;
    private readonly Label _castName;
    private readonly ProgressBar _cast;
    private float _displayScale = 1;

    public void SetDisplayScale(float scale)
    {
        if (MathF.Abs(_displayScale - scale) < 0.001f) return;
        _displayScale = scale;
        SetWidth = MinWidth = 420 * scale;
        SeparationOverride = (int)MathF.Round(3 * scale);
        var font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"),
            Math.Max(1, (int)MathF.Round(13 * scale)));
        _name.FontOverride = _castName.FontOverride = font;
        _name.MinHeight = _name.SetHeight = 22 * scale;
        _health.MinWidth = _health.SetWidth = 420 * scale;
        _health.MinHeight = _health.SetHeight = 14 * scale;
        _casting.MinWidth = _casting.SetWidth = 280 * scale;
        _casting.MinHeight = _casting.SetHeight = 38 * scale;
        _casting.SeparationOverride = (int)MathF.Round(2 * scale);
        _cast.MinWidth = _cast.SetWidth = 280 * scale;
        _cast.MinHeight = _cast.SetHeight = 8 * scale;
        _castName.MinHeight = _castName.SetHeight = 20 * scale;
    }

    public BossEncounterHealthBarUI()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalAlignment = HAlignment.Right;
        SetWidth = MinWidth = 420;
        SeparationOverride = 3;
        var font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 13);
        _name = new Label
        {
            FontOverride = font,
            FontColorOverride = Color.White,
            ClipText = false,
            HorizontalAlignment = HAlignment.Left,
            MinHeight = 22,
            SetHeight = 22
        };
        _health = Bar(420, 14, Color.FromHex("#BF1429"));
        _casting = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 2,
            MinWidth = 280,
            SetWidth = 280,
            MinHeight = 38,
            SetHeight = 38
        };
        _cast = Bar(280, 8, Color.FromHex("#D7AD55"));
        _castName = new Label
        {
            FontOverride = font,
            FontColorOverride = Color.White,
            ClipText = false,
            MinHeight = 20,
            SetHeight = 20
        };
        AddChild(_name);
        AddChild(_health);
        _casting.AddChild(_cast);
        _casting.AddChild(_castName);
        AddChild(_casting);
    }

    private static ProgressBar Bar(float width, float height, Color color)
        => new()
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            SetWidth = width,
            MinWidth = width,
            SetHeight = height,
            MinHeight = height,
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalAlignment = HAlignment.Right,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#201B26") },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color }
        };

    public void SetData(float ratio, float deltaTime, string? bossName, Color? color = null)
    {
        _name.Text = bossName ?? string.Empty;
        _health.Value = Math.Clamp(ratio, 0, 1);
        if (_health.ForegroundStyleBoxOverride is StyleBoxFlat foreground && color is { } tint)
            foreground.BackgroundColor = tint;
    }

    public void SetCast(bool visible, float ratio, string name)
    {
        _cast.Visible = visible;
        _castName.Visible = visible;
        _cast.Value = Math.Clamp(ratio, 0, 1);
        _castName.Text = visible ? name.Replace('\r', ' ').Replace('\n', ' ') : string.Empty;
    }
}
