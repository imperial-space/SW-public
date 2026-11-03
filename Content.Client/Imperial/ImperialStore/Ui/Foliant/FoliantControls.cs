using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.ImperialStore.Ui.Foliant;

/// <summary>
///     imperial medieval - a window without a frame or a title: the whole window is taken to move it. BaseWindow is
///     abstract and the xaml needs a root it can create, so the book stands on this one.
/// </summary>
[Virtual]
public class FoliantWindow : BaseWindow
{
    public FoliantWindow()
    {
        Resizable = false;
        // without this the window is not hit by a click and cannot be taken
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.Move;
}

/// <summary>
///     imperial medieval - a ribbon bookmark: the name of a school and what is left of its essence.
/// </summary>
public sealed class FoliantRibbon : ContainerButton
{
    private const float ChosenHeight = 78;

    private readonly StyleBoxTexture _normal;
    private readonly StyleBoxTexture _hover;
    private readonly StyleBoxTexture? _trim;

    public FoliantRibbon(IResourceCache cache, string name, string? school, string amount, Color tint, Color color, bool chosen, string theme = "")
    {
        _trim = FoliantStyle.RibbonTrim(cache, theme);
        _normal = FoliantStyle.Ribbon(cache, tint, theme);
        _hover = FoliantStyle.Ribbon(cache, new Color(
            Math.Min(1f, tint.R * 1.2f),
            Math.Min(1f, tint.G * 1.2f),
            Math.Min(1f, tint.B * 1.2f)), theme);
        StyleBoxOverride = _normal;

        // the chosen one hangs lower, the others are as tall as their text
        if (chosen)
            MinHeight = ChosenHeight;
        MinWidth = 74;
        // ribbons of different length hang side by side, none is stretched to the longest
        VerticalAlignment = VAlignment.Top;

        var amountRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 4,
        };
        amountRow.AddChild(FoliantStyle.Text(amount, null, FoliantStyle.Cloth));
        amountRow.AddChild(FoliantStyle.Rune(cache, school, color, 1));

        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        column.AddChild(new Label
        {
            Text = name,
            FontOverride = FoliantStyle.Vinque(cache, 14),
            FontColorOverride = FoliantStyle.Cloth,
            Align = Label.AlignMode.Center,
        });
        column.AddChild(amountRow);
        AddChild(column);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // over the cloth, under the text (the children are drawn after the parent)
        _trim?.Draw(handle, PixelSizeBox, UIScale);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (_normal == null || _hover == null)
            return;

        StyleBoxOverride = DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed ? _hover : _normal;
    }
}

/// <summary>
///     imperial medieval - a leather button with a brass rim, the text and then, if there is any, the price.
/// </summary>
public sealed class FoliantButton : ContainerButton
{
    private readonly StyleBoxTexture _normal;
    private readonly StyleBoxTexture _hover;
    private readonly StyleBoxTexture _off;

    public readonly Label Title;
    public readonly BoxContainer CostBox;

    private readonly FoliantStyle.Palette _palette;

    public FoliantButton(IResourceCache cache, string text, string theme = "")
    {
        _palette = FoliantStyle.PaletteOf(theme);
        _normal = FoliantStyle.Button(cache, "button.png", theme);
        _hover = FoliantStyle.Button(cache, "button_hover.png", theme);
        _off = FoliantStyle.Button(cache, "button_off.png", theme);
        StyleBoxOverride = _normal;

        Title = new Label
        {
            Text = text,
            FontOverride = FoliantStyle.Vinque(cache, 14),
            FontColorOverride = _palette.ButtonText,
            VerticalAlignment = VAlignment.Center,
        };
        CostBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
            VerticalAlignment = VAlignment.Center,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(6, 2),
        };
        row.AddChild(Title);
        row.AddChild(CostBox);
        AddChild(row);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (_normal == null || Title == null)
            return;

        switch (DrawMode)
        {
            case DrawModeEnum.Disabled:
                StyleBoxOverride = _off;
                Title.FontColorOverride = FoliantStyle.Faded;
                break;
            case DrawModeEnum.Hover:
            case DrawModeEnum.Pressed:
                StyleBoxOverride = _hover;
                Title.FontColorOverride = _palette.ButtonTextHot;
                break;
            default:
                StyleBoxOverride = _normal;
                Title.FontColorOverride = _palette.ButtonText;
                break;
        }
    }
}

/// <summary>
///     imperial medieval - an inked word on a page that can be pressed.
/// </summary>
public sealed class FoliantLink : ContainerButton
{
    private readonly Label _label;

    public FoliantLink(IResourceCache cache, string text)
    {
        StyleBoxOverride = new StyleBoxEmpty();

        _label = new Label
        {
            Text = text,
            FontOverride = FoliantStyle.Vinque(cache, 14),
            FontColorOverride = FoliantStyle.InkRed,
        };
        AddChild(_label);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (_label == null)
            return;

        _label.FontColorOverride = DrawMode switch
        {
            DrawModeEnum.Disabled => FoliantStyle.InkFaded,
            DrawModeEnum.Hover or DrawModeEnum.Pressed => FoliantStyle.LinkHover,
            _ => FoliantStyle.InkRed,
        };
    }
}

/// <summary>
///     imperial medieval - a row of a list that is lit while the mouse is over it, even when the mouse is over a button in it.
/// </summary>
public sealed class FoliantRow : PanelContainer
{
    private static readonly Color Lit = new(0.82f, 0.75f, 0.59f, 0.45f);

    private readonly Control _view;
    private readonly IInputManager _input;
    private bool _lit;

    public FoliantRow(Control view)
    {
        _view = view;
        _input = IoCManager.Resolve<IInputManager>();
        PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var mouse = (Vector2i) _input.MouseScreenPosition.Position;
        var lit = VisibleInTree && GlobalPixelRect.Contains(mouse) && _view.GlobalPixelRect.Contains(mouse);

        if (lit == _lit)
            return;

        _lit = lit;
        PanelOverride = new StyleBoxFlat { BackgroundColor = lit ? Lit : Color.Transparent };
    }
}

/// <summary>
///     imperial medieval - a socket of the ritual circle, a glow shows that it can be touched.
/// </summary>
public sealed class FoliantSocketButton : ContainerButton
{
    public Control? Halo;
    public bool Picked;

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (Halo != null)
            Halo.Visible = Picked || DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed;
    }
}

/// <summary>
///     imperial medieval - what a slot of the grimoire shows.
/// </summary>
public readonly record struct FoliantSocket(string? School, Color Color, string Tooltip);

/// <summary>
///     imperial medieval - the essence slots as a ritual circle: a ring, a star drawn in ink and a seal on every point.
/// </summary>
public sealed class FoliantRitual : Control
{
    public const float AreaWidth = 320;
    public const float AreaHeight = 208;

    private const float Radius = 80;

    // the star is taller above its centre than below it, the circle is moved down to stand in the middle
    private const float CenterShift = 8;
    private const float SealSize = 60;

    public event Action<int>? OnPressed;

    private readonly int _count;

    private readonly IGameTiming _timing;
    private readonly (int Slot, Color Color, TimeSpan Start)? _flash;

    private const float FlashTime = 0.8f;

    public FoliantRitual(IResourceCache cache, IReadOnlyList<FoliantSocket> sockets, int? picked, (int Slot, Color Color, TimeSpan Start)? flash, string theme = "")
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        _flash = flash;
        _count = sockets.Count;
        MinSize = new Vector2(AreaWidth, AreaHeight);
        HorizontalAlignment = HAlignment.Center;

        for (var i = 0; i < sockets.Count; i++)
        {
            var index = i;
            var socket = sockets[i];
            var at = Point(i, 1, sockets.Count);

            var face = new Control { MinSize = new Vector2(SealSize, SealSize) };

            var halo = new TextureRect
            {
                Texture = FoliantStyle.Tex(cache, "halo.png"),
                Stretch = TextureRect.StretchMode.Keep,
                Visible = picked == i,
            };
            face.AddChild(halo);

            face.AddChild(new TextureRect
            {
                Texture = FoliantStyle.Tex(cache, theme, "socket.png"),
                Stretch = TextureRect.StretchMode.Keep,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });

            if (socket.School != null)
            {
                var rune = FoliantStyle.Rune(cache, socket.School, socket.Color, 2);
                rune.HorizontalAlignment = HAlignment.Center;
                face.AddChild(rune);
            }

            var button = new FoliantSocketButton
            {
                Halo = halo,
                Picked = picked == i,
                StyleBoxOverride = new StyleBoxEmpty(),
                HorizontalAlignment = HAlignment.Left,
                VerticalAlignment = VAlignment.Top,
                Margin = new Thickness(at.X - SealSize / 2, at.Y - SealSize / 2, 0, 0),
                ToolTip = socket.Tooltip,
            };
            button.AddChild(face);
            button.OnPressed += _ => OnPressed?.Invoke(index);

            AddChild(button);
        }
    }

    /// <summary>A point of the star, <paramref name="scale"/> is the part of the radius.</summary>
    private static Vector2 Point(int index, float scale, int count = 5)
    {
        var angle = -MathF.PI / 2 + index * MathF.Tau / count;
        return new Vector2(AreaWidth / 2 + MathF.Cos(angle) * Radius * scale, AreaHeight / 2 + CenterShift + MathF.Sin(angle) * Radius * scale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var center = new Vector2(AreaWidth / 2, AreaHeight / 2 + CenterShift) * UIScale;
        handle.DrawCircle(center, Radius * UIScale, FoliantStyle.RitualLine, false);

        // a ring spreads from the socket that has just been filled
        if (_flash is { } flash)
        {
            var t = (float) (_timing.RealTime - flash.Start).TotalSeconds / FlashTime;
            if (t is >= 0 and < 1)
            {
                var color = flash.Color.WithAlpha(1 - t);
                var at = Point(flash.Slot, 1, _count) * UIScale;
                handle.DrawCircle(at, (SealSize / 2 - 6 + t * 26) * UIScale, color, false);
                handle.DrawCircle(at, (SealSize / 2 - 10 + t * 18) * UIScale, color.WithAlpha((1 - t) * 0.5f), false);
            }
        }

        // every point is joined with the second one after it
        for (var i = 0; i < _count; i++)
        {
            handle.DrawLine(Point(i, 1, _count) * UIScale, Point((i + 2) % _count, 1, _count) * UIScale, FoliantStyle.RitualLine);
        }
    }
}
