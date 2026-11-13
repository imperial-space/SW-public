using System.Numerics;
using Content.Client.Imperial.Medieval.UserInterface.Elements;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - what one cooking pot holds, drawn as small icons floating above it. Equal ingredients
///     share an icon with a count, a click on an icon takes one out, and the finished dish takes their place.
///     All sizes are fractions of a map tile on screen, so the icons keep their place when the view is zoomed.
/// </summary>
public sealed class MedievalPotIcons : LayoutContainer
{
    public struct Entry
    {
        public string Prototype;
        public string Name;
        public EntityUid Item;
        public int Count;
    }

    private const float WidthTiles = 4f;
    private const float HeightTiles = 2.1f;
    private const float IconTiles = 0.5f;
    // the bowl sprite has more empty margin than the ingredients, so it needs a bigger box to look as large
    private const float DishTiles = 0.7f;
    private const float PitchTiles = 0.41f;
    private const float RowTiles = 0.9f;
    private const float MouthTiles = 0.1f;
    private const float BobTiles = 0.035f;
    private const float PipWidthTiles = 0.094f;
    private const float PipHeightTiles = 0.0625f;
    private const float PipGapTiles = 0.031f;
    private const float PipsBelowRowTiles = 0.31f;
    private const int MaxPips = 6;

    private const float FallTime = 0.32f;
    private const float PopTime = 0.45f;
    private const float SinkTime = 0.35f;
    private const float PluckTime = 0.15f;
    private const float BumpTime = 0.2f;
    private const float WaterShowTime = 2.5f;
    private const float FadeSpeed = 8f;
    private const float SlideSpeed = 12f;
    private const float PixelsPerTile = 32f;
    private const float TileTolerance = 0.01f;
    private const float BobSpeed = 2.4f;
    private const float BobPhaseStep = 1.3f;
    private const float BumpGrowth = 0.2f;
    private const float DropStartTiles = 1.1f;
    private const float DropArcTiles = 0.3f;

    private static readonly StyleBoxFlat WaterFull = new() { BackgroundColor = Color.FromHex("#5b95c9") };
    private static readonly StyleBoxFlat WaterEmpty = new() { BackgroundColor = Color.FromHex("#2f241c") };

    private readonly List<Icon> _icons = new();
    private readonly List<Drop> _drops = new();
    private readonly LayoutContainer _water = new() { MouseFilter = MouseFilterMode.Ignore };
    private readonly PanelContainer[] _pips = new PanelContainer[MaxPips];
    private readonly EntityPrototypeView _dish = NewView();
    private readonly CountLabel _portions = NewCount();

    private float _tile;
    private float _time;
    private bool _synced;
    private bool _dishSynced;
    private string? _dishId;
    private int _portionsShown = -1;
    private float _dishAge;
    private int _waterShown = -1;
    private int _maxWaterShown = -1;
    private float _waterTimer;
    private float _waterAlpha;
    private bool _potHovered;

    /// <summary>
    ///     Asks to take an item out; false when nothing was asked for, e.g. the player's hands are full.
    /// </summary>
    public Func<EntityUid, bool>? TryTake;

    /// <summary>
    ///     Icons only catch the mouse while the player could take something. Otherwise clicks go through
    ///     them to the world, so a player holding a tool can still use it on what stands behind.
    /// </summary>
    public bool Interactive;

    /// <summary>
    ///     Set by the system that owns this control: when the pot was last checked for being in sight.
    /// </summary>
    public TimeSpan NextSightCheck;

    /// <summary>
    ///     What the contents looked like at the last sync, so an unchanged pot is not gone through again.
    /// </summary>
    public int Signature = -1;

    public MedievalPotIcons()
    {
        MouseFilter = MouseFilterMode.Ignore;

        for (var i = 0; i < MaxPips; i++)
        {
            _pips[i] = new PanelContainer { MouseFilter = MouseFilterMode.Ignore };
            _water.AddChild(_pips[i]);
        }

        _water.Modulate = Color.Transparent;
        AddChild(_water);

        _dish.Visible = false;
        _portions.Visible = false;
        AddChild(_dish);
        AddChild(_portions);
    }

    /// <summary>
    ///     Puts the control over the pot. <paramref name="tile"/> is the size of a map tile on screen.
    /// </summary>
    public void Place(Vector2 potOnScreen, float tile)
    {
        if (MathF.Abs(tile - _tile) > TileTolerance)
        {
            _tile = tile;
            SetSize = new Vector2(WidthTiles, HeightTiles) * tile;
            _dish.SetSize = new Vector2(DishTiles, DishTiles) * tile;
            ResizePips();

            foreach (var icon in _icons)
            {
                icon.Resize(IconTiles * tile);
            }
        }

        var corner = potOnScreen - new Vector2(WidthTiles / 2f, HeightTiles) * tile;
        SetPosition(this, new Vector2(MathF.Round(corner.X), MathF.Round(corner.Y)));
    }

    /// <summary>
    ///     The first sync after the control appears shows the contents as they are; later ones animate the change.
    /// </summary>
    public void SetContents(List<Entry> entries)
    {
        foreach (var icon in _icons)
        {
            icon.Kept = false;
        }

        foreach (var entry in entries)
        {
            var icon = Find(entry.Prototype);
            if (icon == null)
            {
                icon = new Icon(entry.Prototype, IconTiles * _tile);
                icon.Pressed = item => TryTake?.Invoke(item) ?? false;
                icon.Age = _synced ? -FallTime : PopTime;
                icon.Phase = _icons.Count * BobPhaseStep;
                _icons.Add(icon);
                AddChild(icon);

                if (_synced)
                    AddDrop(entry.Prototype);
            }
            else if (entry.Count > icon.Count && _synced)
            {
                icon.BumpAge = -FallTime;
                AddDrop(entry.Prototype);
            }

            icon.Kept = true;
            icon.Set(entry);
        }

        foreach (var icon in _icons)
        {
            if (!icon.Kept && icon.Leaving == LeaveKind.None)
                icon.Leave(LeaveKind.Pluck);
        }

        _synced = true;
        // placed now, not on the next frame, so a new icon is never drawn in the corner first
        Animate(0f);
    }

    public void SetDish(string? result, int portions)
    {
        if (portions != _portionsShown)
        {
            _portionsShown = portions;
            _portions.Text = portions.ToString();
        }

        var first = !_dishSynced;
        _dishSynced = true;
        if (result == _dishId)
            return;

        var cooked = _dishId == null && result != null && !first;
        _dishId = result;
        _dish.SetPrototype(result);

        if (!cooked)
        {
            // already there when the player came, or one dish turning into another as it burns
            _dishAge = first ? PopTime : 0f;
            return;
        }

        // the ingredients go down into the pot, and the dish comes up once they are gone
        _dishAge = -SinkTime;
        foreach (var icon in _icons)
        {
            if (icon.Leaving == LeaveKind.None)
                icon.Leave(LeaveKind.Sink);
        }
    }

    /// <summary>
    ///     Water is only shown while it changes or while the pot is being pointed at.
    /// </summary>
    public void SetWater(int water, int max, bool potHovered)
    {
        _potHovered = potHovered;
        if (water == _waterShown && max == _maxWaterShown)
            return;

        if (_waterShown >= 0)
            _waterTimer = WaterShowTime;

        _waterShown = water;
        _maxWaterShown = max;
        max = Math.Clamp(max, 0, MaxPips);

        for (var i = 0; i < MaxPips; i++)
        {
            _pips[i].Visible = i < max;
            _pips[i].PanelOverride = i < water ? WaterFull : WaterEmpty;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Animate(args.DeltaSeconds);
    }

    private void Animate(float delta)
    {
        if (_tile <= 0f)
            return;

        _time += delta;

        var centre = WidthTiles / 2f * _tile;
        var row = (HeightTiles - RowTiles) * _tile;
        var mouth = (HeightTiles - MouthTiles) * _tile;
        var size = IconTiles * _tile;

        RemoveGoneIcons();
        SlideIcons(delta, centre);

        foreach (var icon in _icons)
        {
            AnimateIcon(icon, delta, centre, row, mouth, size);
        }

        UpdateDrops(delta, centre, mouth);
        UpdateDish(delta, centre, row, mouth);
        UpdateWater(delta, centre, row);
    }

    private void RemoveGoneIcons()
    {
        for (var i = _icons.Count - 1; i >= 0; i--)
        {
            var icon = _icons[i];
            if (icon.Leaving == LeaveKind.None || icon.LeaveAge < LeaveTime(icon.Leaving))
                continue;

            _icons.RemoveAt(i);
            RemoveChild(icon);
        }
    }

    /// <summary>
    ///     The icons that stay share the row evenly and slide to their new places when one comes or goes.
    /// </summary>
    private void SlideIcons(float delta, float centre)
    {
        var staying = 0;
        foreach (var icon in _icons)
        {
            if (icon.Leaving == LeaveKind.None)
                staying++;
        }

        var index = 0;
        foreach (var icon in _icons)
        {
            if (icon.Leaving != LeaveKind.None)
                continue;

            var target = centre + (index - (staying - 1) / 2f) * PitchTiles * _tile;
            icon.X = icon.Placed ? MathHelper.Lerp(icon.X, target, Math.Min(1f, delta * SlideSpeed)) : target;
            icon.Placed = true;
            index++;
        }
    }

    private void AnimateIcon(Icon icon, float delta, float centre, float row, float mouth, float size)
    {
        icon.Age += delta;
        icon.BumpAge += delta;

        var x = icon.X;
        var y = row + MathF.Sin(_time * BobSpeed + icon.Phase) * BobTiles * _tile;
        var scale = 1f;

        if (icon.Leaving != LeaveKind.None)
        {
            icon.LeaveAge += delta;
            var t = Math.Clamp(icon.LeaveAge / LeaveTime(icon.Leaving), 0f, 1f);
            scale = 1f - t;

            if (icon.Leaving == LeaveKind.Sink)
            {
                x = MathHelper.Lerp(icon.X, centre, t * t);
                y = MathHelper.Lerp(row, mouth, t * t);
            }
            else
            {
                y -= t * size / 2f;
            }
        }
        else if (icon.Age < PopTime)
        {
            // still waiting for its item to fall into the pot, or rising out of it
            var t = Math.Clamp(icon.Age / PopTime, 0f, 1f);
            x = MathHelper.Lerp(centre, icon.X, EaseOut(t));
            y = MathHelper.Lerp(mouth, y, EaseOut(t));
            scale = icon.Age < 0f ? 0f : EaseOutBack(t);
        }
        else if (icon.BumpAge >= 0f && icon.BumpAge < BumpTime)
        {
            scale = 1f + MathF.Sin(icon.BumpAge / BumpTime * MathF.PI) * BumpGrowth;
        }

        icon.Show(scale * _tile / PixelsPerTile * IconTiles, Interactive);
        SetPosition(icon, new Vector2(x - size / 2f, y - size / 2f));
    }

    private void UpdateDrops(float delta, float centre, float mouth)
    {
        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            drop.Age += delta;
            if (drop.Age >= FallTime)
            {
                _drops.RemoveAt(i);
                RemoveChild(drop.View);
                continue;
            }

            // an arc from the upper right down into the pot, shrinking from the item's real size
            var t = drop.Age / FallTime;
            var eased = t * t;
            var x = MathHelper.Lerp(centre + _tile, centre, eased);
            var y = MathHelper.Lerp(mouth - DropStartTiles * _tile, mouth, eased) - MathF.Sin(eased * MathF.PI) * DropArcTiles * _tile;
            var scale = MathHelper.Lerp(1f, 0.5f, eased) * _tile / PixelsPerTile;

            drop.View.Scale = new Vector2(scale, scale);
            SetPosition(drop.View, new Vector2(x - _tile / 2f, y - _tile / 2f));
        }
    }

    private void UpdateDish(float delta, float centre, float row, float mouth)
    {
        if (_dishId == null)
        {
            _dish.Visible = false;
            _portions.Visible = false;
            return;
        }

        _dishAge += delta;
        var t = Math.Clamp(_dishAge / PopTime, 0f, 1f);
        var y = MathHelper.Lerp(mouth, row, EaseOut(t));
        var scale = _dishAge < 0f ? 0f : EaseOutBack(t);

        _dish.Visible = _dishAge >= 0f;
        var dishSize = DishTiles * _tile;
        _dish.Scale = new Vector2(scale, scale) * (_tile / PixelsPerTile * DishTiles);
        SetPosition(_dish, new Vector2(centre - dishSize / 2f, y - dishSize / 2f));

        _portions.Visible = _dishAge >= PopTime && _portionsShown > 1;
        SetPosition(_portions, new Vector2(centre + dishSize * 0.3f, row));
    }

    private void UpdateWater(float delta, float centre, float row)
    {
        _waterTimer = Math.Max(0f, _waterTimer - delta);

        var hovered = _potHovered;
        foreach (var icon in _icons)
        {
            hovered |= icon.Hovered;
        }

        var target = hovered || _waterTimer > 0f ? 1f : 0f;
        _waterAlpha = MathHelper.Lerp(_waterAlpha, target, Math.Min(1f, delta * FadeSpeed));
        _water.Modulate = Color.White.WithAlpha(_waterAlpha);

        var count = Math.Clamp(_maxWaterShown, 0, MaxPips);
        var width = count * PipWidthTiles * _tile + Math.Max(0, count - 1) * PipGapTiles * _tile;
        SetPosition(_water, new Vector2(centre - width / 2f, row + PipsBelowRowTiles * _tile));
    }

    private void ResizePips()
    {
        var pip = new Vector2(PipWidthTiles, PipHeightTiles) * _tile;
        for (var i = 0; i < MaxPips; i++)
        {
            _pips[i].SetSize = pip;
            SetPosition(_pips[i], new Vector2(i * (PipWidthTiles + PipGapTiles) * _tile, 0));
        }
    }

    private void AddDrop(string prototype)
    {
        var view = NewView();
        view.SetSize = new Vector2(_tile, _tile);
        view.SetPrototype(prototype);
        _drops.Add(new Drop(view));
        AddChild(view);
    }

    private Icon? Find(string prototype)
    {
        foreach (var icon in _icons)
        {
            if (icon.Prototype == prototype && icon.Leaving == LeaveKind.None)
                return icon;
        }

        return null;
    }

    private static float LeaveTime(LeaveKind kind)
    {
        return kind == LeaveKind.Sink ? SinkTime : PluckTime;
    }

    private static float EaseOut(float t)
    {
        var inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    /// <summary>
    ///     Overshoots a little before settling, which is what makes an icon "pop".
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.9f;
        var shifted = t - 1f;
        return shifted * shifted * ((overshoot + 1f) * shifted + overshoot) + 1f;
    }

    private static EntityPrototypeView NewView()
    {
        return new EntityPrototypeView
        {
            Stretch = SpriteView.StretchMode.None,
            MouseFilter = MouseFilterMode.Ignore,
        };
    }

    private static CountLabel NewCount()
    {
        return new CountLabel();
    }

    /// <summary>
    ///     A pixel-font number with a one pixel shadow. <see cref="Label"/> has no shadow of its own, so the shadow is a
    ///     second label under the first.
    /// </summary>
    private sealed class CountLabel : Control
    {
        private const string FontPath = "/Fonts/Boxfont-round/Boxfont Round.ttf";
        private const int FontSize = 10;
        private const float ShadowOffset = 1f;

        private static readonly Color ShadowColor = Color.FromHex("#160e0a");

        private readonly Label _shadow;
        private readonly Label _text;

        public string? Text
        {
            set
            {
                _text.Text = value;
                _shadow.Text = value;
            }
        }

        public CountLabel()
        {
            MouseFilter = MouseFilterMode.Ignore;

            var font = IoCManager.Resolve<IResourceCache>().GetFont(FontPath, FontSize);
            _shadow = new Label
            {
                FontOverride = font,
                FontColorOverride = ShadowColor,
                Margin = new Thickness(ShadowOffset, ShadowOffset, 0, 0),
                MouseFilter = MouseFilterMode.Ignore,
            };
            _text = new Label
            {
                FontOverride = font,
                FontColorOverride = MedievalUiStyle.GoldBright,
                MouseFilter = MouseFilterMode.Ignore,
            };

            AddChild(_shadow);
            AddChild(_text);
        }
    }

    private enum LeaveKind : byte
    {
        None,
        Pluck,
        Sink
    }

    private sealed class Drop(EntityPrototypeView view)
    {
        public readonly EntityPrototypeView View = view;
        public float Age;
    }

    /// <summary>
    ///     One kind of ingredient: its sprite, how many there are and the one a click takes out.
    /// </summary>
    private sealed class Icon : Control
    {
        private readonly EntityPrototypeView _view = NewView();
        private readonly CountLabel _count = NewCount();
        private EntityUid _item;

        public readonly string Prototype;
        public int Count;
        public bool Kept;
        public bool Placed;
        public bool Hovered;
        public float X;
        public float Phase;
        public float Age;
        public float BumpAge = BumpTime;
        public LeaveKind Leaving;
        public float LeaveAge;

        public Func<EntityUid, bool>? Pressed;

        public Icon(string prototype, float size)
        {
            Prototype = prototype;
            MouseFilter = MouseFilterMode.Ignore;

            _view.SetPrototype(prototype);
            AddChild(_view);

            _count.HorizontalAlignment = HAlignment.Right;
            _count.VerticalAlignment = VAlignment.Bottom;
            AddChild(_count);

            Resize(size);
        }

        public void Resize(float size)
        {
            SetSize = new Vector2(size, size);
            _view.SetSize = SetSize;
        }

        public void Set(Entry entry)
        {
            _item = entry.Item;
            if (entry.Count == Count && ToolTip != null)
                return;

            Count = entry.Count;
            _count.Text = entry.Count.ToString();
            ToolTip = entry.Count > 1 ? $"{entry.Name} x{entry.Count}" : entry.Name;
        }

        public void Leave(LeaveKind kind)
        {
            Leaving = kind;
            LeaveAge = 0f;
        }

        public void Show(float scale, bool interactive)
        {
            _view.Scale = new Vector2(scale, scale);

            // nothing to click or count while the item is still falling in or the icon is on its way out
            var settled = Leaving == LeaveKind.None && Age >= 0f;
            _count.Visible = settled && Count > 1;

            var filter = settled && interactive ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
            if (MouseFilter == filter)
                return;

            MouseFilter = filter;
            Hovered = false;
        }

        protected override void MouseEntered()
        {
            base.MouseEntered();
            Hovered = true;
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            Hovered = false;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (Hovered)
                handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), MedievalUiStyle.Gold, false);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);

            if (args.Function != EngineKeyFunctions.UIClick || Leaving != LeaveKind.None)
                return;

            if (Pressed?.Invoke(_item) == true)
                args.Handle();
        }
    }
}
