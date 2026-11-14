using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossRaidHudControl : LayoutContainer
{
    private const float PanelWidth = 200;
    private const float HeaderHeight = 24;
    private const float RowTop = 26;
    private const float RowStride = 34;
    private const float BarLeft = 36;
    private const float BarWidth = 161;
    private const float FullHeight = RowTop + 8 * RowStride - 2;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly RaidDragHandle _header;
    private readonly RaidHeaderButton _smaller;
    private readonly RaidHeaderButton _larger;
    private readonly RaidHeaderButton _toggle;
    private readonly RaidHeaderButton _lock;
    private readonly BossRaidPortraitControl[] _portraits = new BossRaidPortraitControl[8];
    private readonly Label[] _names = new Label[8];
    private readonly Label[] _healthPercent = new Label[8];
    private List<BossRaidMember> _members = new();
    private Font _font = default!;
    private Font _smallFont = default!;
    private float _scale = 1;
    private float _fontScale;
    private bool _hidden;
    private bool _locked;
    private bool _moved;
    private bool _rescaled;
    private Vector2 _viewport;

    public BossRaidHudControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalAlignment = HAlignment.Left;
        VerticalAlignment = VAlignment.Top;
        InheritChildMeasure = false;
        _header = new RaidDragHandle(this) { ToolTip = Loc.GetString("boss-raid-drag") };
        _smaller = new RaidHeaderButton();
        _larger = new RaidHeaderButton();
        _toggle = new RaidHeaderButton();
        _lock = new RaidHeaderButton { ToggleMode = true };
        _smaller.OnPressed += _ => Resize(-0.25f);
        _larger.OnPressed += _ => Resize(0.25f);
        _toggle.OnPressed += _ => { _hidden = !_hidden; ArrangeHud(); };
        _lock.OnToggled += args =>
        {
            _locked = args.Pressed;
            if (_locked) _moved = true;
            _header.StopDragging();
            ArrangeHud();
        };
        AddChild(_header);
        AddChild(_smaller);
        AddChild(_larger);
        AddChild(_lock);
        AddChild(_toggle);
        for (var i = 0; i < _portraits.Length; i++)
        {
            _portraits[i] = new BossRaidPortraitControl();
            _names[i] = MakeLabel();
            _healthPercent[i] = MakeLabel();
            _healthPercent[i].Align = Label.AlignMode.Right;
            AddChild(_portraits[i]);
            AddChild(_names[i]);
            AddChild(_healthPercent[i]);
        }
        ArrangeHud();
    }

    private static Label MakeLabel() => new()
    {
        ClipText = true,
        MouseFilter = MouseFilterMode.Ignore,
        HorizontalAlignment = HAlignment.Left,
        VerticalAlignment = VAlignment.Top,
        VAlign = Label.VAlignMode.Center
    };

    public void SetMembers(List<BossRaidMember> members)
    {

        _members = members.GetRange(0, Math.Min(members.Count, _portraits.Length));
        ArrangeHud();
    }

    private void Resize(float delta)
    {
        _rescaled = true;
        _scale = Math.Clamp(_scale + delta, 0.5f, 2);
        ArrangeHud();
        ClampPosition();
    }

    public void UpdateViewport(Vector2 size)
    {
        if (size.X <= 0 || size.Y <= 0) return;
        _viewport = size;
        if (!_rescaled)
        {
            var scale = Math.Clamp(MathF.Min(1, MathF.Min(size.X * 0.1138f / PanelWidth, size.Y * 0.426f / FullHeight)), 0.5f, 1);
            if (!MathHelper.CloseToPercent(_scale, scale))
            {
                _scale = scale;
                ArrangeHud();
            }
        }
        if (!_moved)
            SetPosition(this, new(size.X * 0.936f - SetSize.X, size.Y * 0.342f));
        ClampPosition();
    }

    private void ArrangeHud()
    {
        if (_fontScale != _scale)
        {
            var resource = _resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
            _font = new VectorFont(resource, Math.Max(5, (int)MathF.Round(9 * _scale)));
            _smallFont = new VectorFont(resource, Math.Max(5, (int)MathF.Round(8 * _scale)));
            _fontScale = _scale;
        }
        SetSize = new Vector2(PanelWidth, _hidden || _members.Count == 0 ? HeaderHeight : RowTop + _members.Count * RowStride - 2) * _scale;

        Place(_header, 0, 0, 76, HeaderHeight);
        Place(_smaller, 78, 0, 18, HeaderHeight);
        Place(_larger, 98, 0, 18, HeaderHeight);
        Place(_lock, 118, 0, 40, HeaderHeight);
        Place(_toggle, 160, 0, 40, HeaderHeight);
        _header.ToolTip = Loc.GetString(_locked ? "boss-raid-locked" : "boss-raid-drag");
        SetCaption(_smaller, "−");
        SetCaption(_larger, "+");
        SetCaption(_lock, Loc.GetString(_locked ? "boss-raid-unlock" : "boss-raid-lock"));
        SetCaption(_toggle, Loc.GetString(_hidden ? "boss-raid-show" : "boss-raid-hide"));
        _lock.ToolTip = Loc.GetString(_locked ? "boss-raid-unlock-tooltip" : "boss-raid-lock-tooltip");
        for (var i = 0; i < _portraits.Length; i++)
        {
            var top = RowTop + i * RowStride;
            Place(_portraits[i], 3, top + 2, 28, 28);
            Place(_names[i], BarLeft, top, BarWidth - 36, 14);
            Place(_healthPercent[i], BarLeft + BarWidth - 34, top, 34, 14);
            var visible = !_hidden && i < _members.Count;
            _portraits[i].Visible = _names[i].Visible = _healthPercent[i].Visible = visible;
            _portraits[i].PortraitScale = _scale * 28 / 32;
            _portraits[i].SetEntity(i < _members.Count ? _members[i].Entity : null);
            _portraits[i].SetDead(i < _members.Count && _members[i].State == MobState.Dead);
            _names[i].FontOverride = _font;
            _healthPercent[i].FontOverride = _smallFont;
            if (i >= _members.Count) continue;
            var member = _members[i];
            var status = member.State == MobState.Dead ? "boss-raid-dead" :
                !member.Connected ? "boss-raid-offline" : null;
            _names[i].Text = status == null ? member.Name : $"{Loc.GetString(status)} · {member.Name}";
            _names[i].FontColorOverride = Color.White.WithAlpha(0.95f);
            _healthPercent[i].Text = member.MaxHealth > 0 ? $"{MathF.Round(Math.Clamp(member.Health / member.MaxHealth, 0, 1) * 100):0}%" : "—";
            _healthPercent[i].FontColorOverride = Color.White.WithAlpha(0.95f);
        }
        Parent?.InvalidateMeasure();
    }

    private void SetCaption(RaidHeaderButton button, string text)
    {
        button.Caption.FontOverride = _smallFont;
        button.Caption.Text = text;
        button.Caption.FontColorOverride = button.Pressed ? Color.Yellow : Color.White.WithAlpha(0.9f);
    }

    private void Place(Control control, float x, float y, float width, float height)
    {
        SetMarginLeft(control, x * _scale);
        SetMarginTop(control, y * _scale);
        SetMarginRight(control, (x + width) * _scale);
        SetMarginBottom(control, (y + height) * _scale);
        control.SetSize = new Vector2(width, height) * _scale;
    }

    internal void ClampPosition()
    {
        if (_viewport == Vector2.Zero) return;
        SetPosition(this, Vector2.Clamp(Position, Vector2.Zero, Vector2.Max(Vector2.Zero, _viewport - SetSize)));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_hidden) return;
        var unit = _scale * UIScale;
        for (var i = 0; i < _members.Count; i++)
        {
            var origin = new Vector2(0, RowTop + i * RowStride) * unit;
            handle.DrawRect(UIBox2.FromDimensions(origin, new Vector2(PanelWidth, RowStride - 2) * unit), new Color(0.02f, 0.04f, 0.08f, 0.22f));
            var member = _members[i];
            var health = origin + new Vector2(BarLeft, 14) * unit;
            var critical = member.State == MobState.Critical;
            DrawBar(handle, health, unit, 8, member.Health, member.MaxHealth,
                critical ? new Color(0.9f, 0.2f, 0.28f, 0.8f) : new Color(0.67f, 0.85f, 0.98f, 0.8f));
            if (critical) DrawHeartbeat(handle, health, unit);
            DrawBar(handle, origin + new Vector2(BarLeft, 24) * unit, unit, 6, member.Mana, member.MaxMana,
                new Color(0.23f, 0.48f, 0.92f, 0.75f));
        }
    }

    private static void DrawBar(DrawingHandleScreen handle, Vector2 position, float unit, float height, float value, float maximum, Color color)
    {
        var bounds = UIBox2.FromDimensions(position, new Vector2(BarWidth, height) * unit);
        handle.DrawRect(bounds, Color.Black.WithAlpha(0.4f));
        var fraction = maximum > 0 ? Math.Clamp(value / maximum, 0, 1) : 0;
        handle.DrawRect(UIBox2.FromDimensions(position, new Vector2(MathF.Round(BarWidth * fraction), height) * unit), color);
        handle.DrawRect(bounds, Color.White.WithAlpha(0.25f), false);
    }

    private void DrawHeartbeat(DrawingHandleScreen handle, Vector2 origin, float unit)
    {
        var time = (float)_timing.RealTime.TotalSeconds;
        var pulse = (MathF.Sin(time * MathF.Tau) + 1) / 2;
        handle.DrawRect(UIBox2.FromDimensions(origin + Vector2.One * unit, new Vector2(BarWidth - 2, 6) * unit),
            new Color(0.4f, 0.04f, 0.09f, 0.18f + pulse * 0.12f));
        handle.DrawRect(UIBox2.FromDimensions(origin + new Vector2(1, 4) * unit, new Vector2(BarWidth - 2, 1) * unit),
            new Color(0.95f, 0.3f, 0.4f, 0.2f));
        var head = (int)(time * 70) % ((int)BarWidth + 80);
        for (var x = 1; x < BarWidth - 1; x++)
        {
            var age = head - x;
            if (age < 0 || age >= 80) continue;
            var y = HeartbeatHeight(age);
            var previous = HeartbeatHeight(age + 1);

            handle.DrawRect(UIBox2.FromDimensions(origin + new Vector2(x, Math.Min(y, previous)) * unit,
                    new Vector2(1, Math.Abs(y - previous) + 1) * unit),
                new Color(1, 0.65f, 0.72f, 0.15f + 0.85f * (1 - age / 80f)));
        }
    }

    private static int HeartbeatHeight(int age) => age switch
    {
        10 or 11 => 3,
        19 => 5,
        20 => 6,
        21 => 1,
        22 => 2,
        24 => 5,
        34 or 37 => 3,
        35 or 36 => 2,
        _ => 4
    };

    [Virtual]
    private class RaidHeaderButton : BaseButton
    {
        public readonly Label Caption = new()
        {
            MouseFilter = MouseFilterMode.Ignore,
            ClipText = true,
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
            VerticalAlignment = VAlignment.Stretch,
            Margin = new Thickness(2, 0, 2, 0)
        };

        public RaidHeaderButton() => AddChild(Caption);

        protected override void Draw(DrawingHandleScreen handle)
        {
            handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, PixelSize),
                Color.Black.WithAlpha(DrawMode == DrawModeEnum.Normal ? 0.3f : 0.55f));
            handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, PixelSize), Color.White.WithAlpha(0.15f), false);
        }
    }

    private sealed class RaidDragHandle(BossRaidHudControl hud) : RaidHeaderButton
    {
        private bool _dragging;
        private Vector2 _dragOffset;
        public void StopDragging() => _dragging = false;

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            for (var y = -2; y <= 2; y += 2)
                handle.DrawLine(new Vector2(10, HeaderHeight / 2 + y) * hud._scale * UIScale,
                    new Vector2(26, HeaderHeight / 2 + y) * hud._scale * UIScale, Color.White.WithAlpha(hud._locked ? 0.2f : 0.55f));
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick || hud._locked) return;
            _dragging = true;
            _dragOffset = args.PointerLocation.Position / UIScale - hud.GlobalPosition;
            args.Handle();
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);
            if (args.Function == EngineKeyFunctions.UIClick) _dragging = false;
        }

        protected override void ControlFocusExited()
        {
            base.ControlFocusExited();
            _dragging = false;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            if (!_dragging || hud._locked || hud.Parent == null) return;
            hud._moved = true;
            SetPosition(hud, args.GlobalPosition - hud.Parent.GlobalPosition - _dragOffset);
            hud.ClampPosition();
        }
    }
}
