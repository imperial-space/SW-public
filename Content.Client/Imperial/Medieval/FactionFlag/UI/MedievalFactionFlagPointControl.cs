using Content.Client.Imperial.Medieval.FactionFlag.Systems;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.FactionFlag.UI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.FactionFlag.UI;

public sealed class MedievalFactionFlagPointControl : TextureButton
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    private readonly ClientMedievalFactionFlagSystem _medievalFlagSys;
    private readonly SpriteSystem _sprite;

    private static readonly ProtoId<ShaderPrototype> OutlineShader = "SelectionOutlineInrange";

    private readonly ShaderInstance _hoverOutline;
    private readonly ShaderInstance _captureOutline;
    private readonly ShaderInstance _selectedOutline;

    private const float CapturingBlinkInterval = 0.25f;

    private static readonly Color CapturingColor = Color.Gold;
    private static readonly Color SelectedColor = Color.Lime;
    private static readonly Color HoverColor = Color.Lime;

    private float _blinkTimer;
    private bool _blinkBright = true;
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            InvalidateMeasure();
        }
    }

    public MedievalFactionFlagPointData Data { get; private set; } = default!;
    public NetEntity Entity => Data.Entity;

    public MedievalFactionFlagPointControl(MedievalFactionFlagPointData data)
    {
        IoCManager.InjectDependencies(this);

        _medievalFlagSys = _entMan.System<ClientMedievalFactionFlagSystem>();
        _sprite = _entMan.System<SpriteSystem>();

        SetSize = new Vector2(40f, 40f);

        var shaderProto = _protoMan.Index(OutlineShader);
        _hoverOutline = shaderProto.InstanceUnique();
        _captureOutline = shaderProto.InstanceUnique();
        _selectedOutline = shaderProto.InstanceUnique();

        _hoverOutline.SetParameter("outline_color", HoverColor);
        _hoverOutline.SetParameter("outline_width", 1f);
        _hoverOutline.SetParameter("outline_fullbright", true);

        _captureOutline.SetParameter("outline_color", CapturingColor);
        _captureOutline.SetParameter("outline_width", 2f);
        _captureOutline.SetParameter("outline_fullbright", true);

        _selectedOutline.SetParameter("outline_color", SelectedColor);
        _selectedOutline.SetParameter("outline_width", 2f);
        _selectedOutline.SetParameter("outline_fullbright", true);

        Update(data);
    }

    public void Update(MedievalFactionFlagPointData data)
    {
        var config = _medievalFlagSys.GetMapConfig();

        Data = data;
        ToolTip = data.Name;

        var icon = config.NeutralPointIcon;

        if (data.Owner != null && config.FactionPointIcons.TryGetValue(data.Owner.Value, out var factionIcon))
            icon = factionIcon;

        TextureNormal = _sprite.Frame0(icon);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_captureOutline == null || Data.State != CapturePointState.Capturing)
            return;

        _blinkTimer += args.DeltaSeconds;
        if (_blinkTimer < CapturingBlinkInterval)
            return;

        _blinkTimer -= CapturingBlinkInterval;
        _blinkBright = !_blinkBright;

        var color = CapturingColor.WithAlpha(_blinkBright ? 1f : 0.65f);

        _captureOutline.SetParameter("outline_color", color);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        ShaderInstance? shader = null;

        if (IsSelected)
            shader = _selectedOutline;
        else if (Data.State == CapturePointState.Capturing)
            shader = _captureOutline;
        else if (DrawMode == DrawModeEnum.Hover)
            shader = _hoverOutline;

        if (shader != null)
            handle.UseShader(shader);

        base.Draw(handle);

        if (shader != null)
            handle.UseShader(null);
    }
}
