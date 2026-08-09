using Content.Client.Imperial.Medieval.FactionFlag.Systems;
using Content.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.FactionFlag.UI;

public sealed partial class MedievalFactionFlagMapControl : MapGridControl
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ClientMedievalFactionFlagSystem _medievalFlagSys;
    private readonly SpriteSystem _sprite;

    protected override bool Draggable => true;

    private readonly List<MapChild> _mapChildren = [];

    private sealed class MapChild
    {
        public required Control Control;
        public Vector2 Position;
        public bool ScaleSizeWithMap;
        public Vector2 MapSize;
    }

    public MedievalFactionFlagMapControl() : base(180f, 1200f, 400f)
    {
        IoCManager.InjectDependencies(this);

        _medievalFlagSys = _entMan.System<ClientMedievalFactionFlagSystem>();
        _sprite = _entMan.System<SpriteSystem>();

        InheritChildMeasure = false;
        WorldRangeChanged += _ => UpdateMapChildren();
        DefaultCursorShape = CursorShape.Move;

        var config = _medievalFlagSys.GetMapConfig();

        var center = config.MapSize / 2f;

        Offset = TargetOffset = center;

        var range = Math.Clamp(
            MathF.Max(config.MapSize.X, config.MapSize.Y) / 2f,
            WorldMinRange,
            WorldMaxRange);

        AddRadarRange(range - WorldRange);
        WorldRange = range;

        AddMapControl(new TextureRect
        {
            Texture = _sprite.Frame0(config.MapTexture),
            Stretch = TextureRect.StretchMode.Scale,
            CanShrink = true,
            MouseFilter = MouseFilterMode.Ignore
        }, center, config.MapSize);
    }

    public void AddMapControl(Control control, Vector2 position)
    {
        AddMapControl(control, position, Vector2.Zero, false);
    }

    public void AddMapControl(Control control, Vector2 position, Vector2 mapSize, bool scaleSizeWithMap = true)
    {
        AddChild(control);
        _mapChildren.Add(new MapChild
        {
            Control = control,
            Position = position,
            MapSize = mapSize,
            ScaleSizeWithMap = scaleSizeWithMap,
        });

        UpdateMapChildren();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        var oldOffset = Offset;

        base.MouseMove(args);

        if (oldOffset != Offset)
            UpdateMapChildren();
    }

    private void UpdateMapChildren()
    {
        foreach (var child in _mapChildren)
        {
            var screenPosition = MapToScreen(child.Position);

            if (child.ScaleSizeWithMap)
            {
                var size = child.MapSize * MinimapScale;

                child.Control.SetSize = size;

                SetPosition(child.Control, screenPosition - size / 2f);
            }
            else
            {
                SetPosition(child.Control, screenPosition - child.Control.SetSize / 2f);
            }
        }
    }

    private Vector2 MapToScreen(Vector2 mapPosition)
    {
        var relative = mapPosition - Offset;

        return ScalePosition(new Vector2(relative.X, -relative.Y));
    }
}
