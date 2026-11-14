using System.Numerics;
using Content.Client.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossRaidPortraitControl : Control
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private static readonly ProtoId<ShaderPrototype> GreyscaleShader = "Greyscale";
    private ShaderInstance? _greyscale;
    private bool _dead;
    public bool IsDead => _dead;
    private static readonly HashSet<HumanoidVisualLayers> HeadParts = new()
    {
        HumanoidVisualLayers.Head, HumanoidVisualLayers.Eyes, HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair, HumanoidVisualLayers.Snout, HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.HeadSide, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.Special
    };
    private static readonly string[] HeadSlots = { "head", "mask", "eyes", "ears" };
    private NetEntity? _entity;
    private readonly List<(Texture Texture, Color Color, Vector2 Scale)> _layers = new();
    public float PortraitScale = 1;

    public BossRaidPortraitControl()
    {
        IoCManager.InjectDependencies(this);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetEntity(NetEntity? entity)
    {
        if (_entity == entity) return;
        _entity = entity;
        _layers.Clear();
    }

    public void SetDead(bool dead)
    {
        if (_dead == dead) return;
        _dead = dead;
        if (dead) _greyscale ??= _prototypes.Index(GreyscaleShader).Instance();
        ModulateSelfOverride = dead ? new Color(0.65f, 0.65f, 0.65f, 0.8f) : null;
    }

    private void CaptureFace()
    {
        if (_layers.Count != 0 || _entity is not { } net || !_entities.TryGetEntity(net, out var resolved) || resolved is not { } uid ||
            !_entities.TryGetComponent(uid, out SpriteComponent? sprite)) return;
        var sprites = _entities.System<SpriteSystem>();
        var headLayers = new HashSet<ISpriteLayer>();
        var humanoid = sprites.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Head, out _, false);
        if (humanoid)
        {
            foreach (var part in HeadParts)
                if (sprites.TryGetLayer((uid, sprite), part, out var layer, false)) headLayers.Add(layer);

            if (_entities.TryGetComponent(uid, out HumanoidAppearanceComponent? appearance))
            {
                foreach (var markings in appearance.MarkingSet.Markings.Values)
                    foreach (var marking in markings)
                    {
                        if (!_prototypes.TryIndex<MarkingPrototype>(marking.MarkingId, out var prototype) ||
                            !HeadParts.Contains(prototype.BodyPart)) continue;
                        foreach (var specifier in prototype.Sprites)
                            if (specifier is SpriteSpecifier.Rsi rsi)
                                AddHeadLayer($"{prototype.ID}-{rsi.RsiState}");
                    }
            }

            _entities.TryGetComponent(uid, out InventorySlotsComponent? inventory);
            foreach (var slot in HeadSlots)
            {
                AddHeadLayer(slot);
                if (inventory != null && inventory.VisualLayerKeys.TryGetValue(slot, out var keys))
                    foreach (var key in keys) AddHeadLayer(key);
            }
        }

        foreach (var layer in sprite.AllLayers)
        {

            if (!layer.Visible || layer is SpriteComponent.Layer { CopyToShaderParameters: not null } ||
                humanoid && !headLayers.Contains(layer)) continue;
            var texture = layer.Texture;
            if (layer.ActualRsi is { } rsi && rsi.TryGetState(layer.RsiState, out var state))
                texture = state.Frame0;
            if (texture != null) _layers.Add((texture, layer.Color, layer.Scale));
        }

        void AddHeadLayer(string key)
        {
            if (sprites.TryGetLayer((uid, sprite), key, out var layer, false)) headLayers.Add(layer);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        CaptureFace();
        var unit = PortraitScale * UIScale;

        var center = new Vector2(16, 40) * unit;
        var previousShader = handle.GetShader();
        if (_dead) handle.UseShader(_greyscale);
        try
        {
            foreach (var layer in _layers)
            {
                var size = layer.Texture.Size * layer.Scale * (8f / 3 * unit);
                var color = layer.Color;

                if (_dead)
                {
                    var grey = color.R * 0.299f + color.G * 0.587f + color.B * 0.114f;
                    color = new Color(grey, grey, grey, color.A);
                }
                handle.DrawTextureRect(layer.Texture, UIBox2.FromDimensions(center - size / 2, size), color);
            }
        }
        finally { if (_dead) handle.UseShader(previousShader); }
    }
}
