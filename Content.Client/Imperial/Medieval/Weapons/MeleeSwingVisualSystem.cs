using System.Linq;
using System.Numerics;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Weapons;

public sealed class MeleeSwingVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, Held> _held = new();
    private MeleeSwingOverlay _swing = default!;
    private sealed record Held(EntityUid User, string[] Layers, HandLocation Hand);

    public override void Initialize()
    {
        SubscribeLocalEvent<MeleeSwingComponent, HeldVisualsUpdatedEvent>(OnHeldUpdated);
        _swing = new MeleeSwingOverlay(EntityManager, _resources, _prototypes, _timing);
        _overlays.AddOverlay(_swing);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<MeleeSwingOverlay>();
        _held.Clear();
        base.Shutdown();
    }

    private void OnHeldUpdated(EntityUid uid, MeleeSwingComponent comp, HeldVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
        {
            _held.Remove(uid);
            return;
        }
        var hand = args.RevealedLayers.Contains("inhand-left") ? HandLocation.Left : HandLocation.Right;
        _held[uid] = new Held(args.User, args.RevealedLayers.ToArray(), hand);   // remember the weapon layers shown in the hands
    }

    public override void FrameUpdate(float frameTime)
    {
        foreach (var (weapon, held) in _held.ToArray())
        {
            if (!Exists(weapon) || !TryComp<SpriteComponent>(held.User, out var sprite))
            {
                _held.Remove(weapon);
                continue;
            }
            if (!TryComp<MeleeSwingComponent>(weapon, out var effect) || !_prototypes.TryIndex(effect.Style, out var style)) continue;
            var wielded = TryComp<WieldableComponent>(weapon, out var wieldable) && wieldable.Wielded;
            ApplyHeldLayers(_sprites, (held.User, sprite), held.Layers, held.Hand, wielded, style);
        }
    }

    public static void ApplyHeldLayers(SpriteSystem sprites, Entity<SpriteComponent?> user,
        string[] keys, HandLocation hand, bool wielded, MeleeSwingStylePrototype style)
    {
        var (front, rear) = MeleeSwingVisuals.HeldStates(style, hand, wielded);
        foreach (var key in keys)
        {
            if (!sprites.LayerMapTryGet(user, key, out var layer, false))
                continue;
            sprites.LayerSetVisible(user, layer, true);

            var state = key.EndsWith("-rear", StringComparison.Ordinal) ? rear : front;
            if (!key.EndsWith("-displacement", StringComparison.Ordinal) && state != null)
                sprites.LayerSetRsiState(user, layer, state);

            sprites.LayerSetScale(user, layer, Vector2.One);
            sprites.LayerSetRotation(user, layer, Angle.Zero);
            sprites.LayerSetOffset(user, layer, Vector2.Zero);
        }

        UpdateLayerOrder(sprites, user, keys.Where(k => k.Contains("-rear", StringComparison.Ordinal)).ToArray(), true);
        UpdateLayerOrder(sprites, user, keys.Where(k => !k.Contains("-rear", StringComparison.Ordinal)).ToArray(), false);
    }

    private static void UpdateLayerOrder(SpriteSystem sprites, Entity<SpriteComponent?> sprite, string[] keys, bool behindBody)
    {
        if (!sprites.LayerMapTryGet(sprite, HumanoidVisualLayers.Chest, out _, false))
            return;

        var ordered = new List<(string Key, int Index)>();
        foreach (var key in keys)
        {
            if (sprites.LayerMapTryGet(sprite, key, out var index, false))
                ordered.Add((key, index));
        }
        ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (var (key, _) in ordered)
        {
            if (!sprites.LayerMapTryGet(sprite, key, out var index, false) ||
                !sprites.LayerMapTryGet(sprite, HumanoidVisualLayers.Chest, out var body, false) ||
                behindBody == (index < body))
                continue;
            if (!sprites.RemoveLayer(sprite, key, out var layer, false))
                continue;

            int? destination = behindBody && sprites.LayerMapTryGet(sprite, HumanoidVisualLayers.Chest, out body, false)
                ? body : null;
            var moved = sprites.AddLayer(sprite, layer, destination);
            sprites.LayerMapSet(sprite, key, moved);
        }
    }

    public void PlaySwing(EntityUid user, EntityUid weapon, Vector2 localAim, bool leftToRight)
    {
        if (!TryComp<MeleeSwingComponent>(weapon, out var effect) || !_prototypes.TryIndex(effect.Style, out var style) ||
            !TryComp<TransformComponent>(user, out var xform) || localAim.LengthSquared() <= 0)
            return;
        var parentRotation = _transform.GetWorldRotation(xform) - xform.LocalRotation;
        var rotation = MeleeSwingVisuals.SwingTextureRotation(localAim, parentRotation);
        var normalLength = TryComp<MeleeWeaponComponent>(weapon, out var melee)
            ? melee.CPAnimationLength : style.ReferenceSwingSeconds;
        _swing.Add(user, weapon, xform.MapID, rotation, localAim.Length(), leftToRight, normalLength, style);
    }
}

public sealed class MeleeSwingOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    private readonly IResourceCache _resources;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    private readonly List<Swing> _swings = new();
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private sealed record Swing(EntityUid Owner, EntityUid Weapon, MapId Map, Angle Facing, float Reach, TimeSpan Start,
        bool LeftToRight, float NormalLength, MeleeSwingStylePrototype Style);

    public MeleeSwingOverlay(IEntityManager entities, IResourceCache resources, IPrototypeManager prototypes, IGameTiming timing)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _resources = resources;
        _timing = timing;
        _shader = prototypes.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = (int)Content.Shared.DrawDepth.DrawDepth.HighFloorObjects;
    }

    public void Add(EntityUid owner, EntityUid weapon, MapId map, Angle facing, float reach, bool leftToRight, float normalLength, MeleeSwingStylePrototype style)
    {
        if (float.IsFinite(reach) && reach > 0 && MeleeSwingVisuals.IsValid(style))
            _swings.Add(new Swing(owner, weapon, map, facing, reach, _timing.CurTime, leftToRight, normalLength, style));
    }

    private bool WeaponActive(Swing swing)
        => _entities.TryGetComponent<MeleeSwingComponent>(swing.Weapon, out var effect) &&
           (!effect.RequireWielded || _entities.TryGetComponent<WieldableComponent>(swing.Weapon, out var wielded) && wielded.Wielded) &&
           _entities.TryGetComponent<TransformComponent>(swing.Weapon, out var held) && held.ParentUid == swing.Owner;

    public static bool TryGetSwingTransform(IEntityManager entities, SharedTransformSystem transform,
        EntityUid owner, MapId map, Angle facing, out Matrix3x2 matrix)
    {
        matrix = Matrix3x2.Identity;
        if (!entities.TryGetComponent<TransformComponent>(owner, out var holder) || holder.MapID != map)
            return false;
        matrix = Matrix3x2.CreateRotation((float)facing) *
            Matrix3x2.CreateTranslation(transform.GetWorldPosition(holder));
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _swings.RemoveAll(s => !_entities.TryGetComponent<TransformComponent>(s.Owner, out var owner) || owner.MapID != s.Map ||
            (_timing.CurTime - s.Start).TotalSeconds >=
            MeleeSwingVisuals.WeaponSwingDuration(s.Style, s.NormalLength));
        if (_swings.Count == 0)
            return;
        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        foreach (var swing in _swings)
        {
            if (swing.Map != args.MapId ||
                !TryGetSwingTransform(_entities, _transform, swing.Owner, swing.Map, swing.Facing, out var matrix))
                continue;
            var style = swing.Style;
            if (!_resources.TryGetResource<RSIResource>(style.Texture, out var resource)) continue;
            var age = (float)(_timing.CurTime - swing.Start).TotalSeconds;
            if (!resource.RSI.TryGetState(MeleeSwingVisuals.SwingState(style, swing.LeftToRight), out var state))
                continue;
            var frame = Math.Min(MeleeSwingVisuals.SwingFrame(style, age, swing.NormalLength), state.DelayCount - 1); // sync effect frames with the attack speed
            var hand = style.HandOffset;
            if (_entities.TryGetComponent<SpriteComponent>(swing.Owner, out var holderSprite))
                hand += holderSprite.Offset;
            var grip = matrix.Translation + (-(args.Viewport.Eye?.Rotation ?? Angle.Zero)).RotateVec(hand);
            matrix.Translation = grip;
            handle.SetTransform(matrix);

            handle.DrawTextureRect(state.GetFrame(RsiDirection.South, frame),
                MeleeSwingVisuals.TextureBounds(resource.RSI.Size, style.AnchorPixels ?? new Vector2(resource.RSI.Size.X, resource.RSI.Size.Y) / 2,
                    style.ReferenceRadiusPixels, MeleeSwingVisuals.SwingDrawRadius(style, swing.Reach)), Color.White);
            var alpha = MeleeSwingVisuals.WeaponAlpha(style, age, swing.NormalLength);
            if (alpha > 0 && WeaponActive(swing) && style.WeaponTexture is { } weaponTexture &&
                _resources.TryGetResource<RSIResource>(weaponTexture, out var weapon) &&
                weapon.RSI.TryGetState(style.WeaponState, out var item))
            {
                var rotation = MeleeSwingVisuals.WeaponRotation(style, swing.Facing, age, swing.NormalLength, swing.LeftToRight);
                handle.SetTransform(Matrix3x2.CreateScale(swing.LeftToRight ? -1 : 1, 1) *
                    Matrix3x2.CreateRotation((float)rotation) * Matrix3x2.CreateTranslation(grip));
                handle.UseShader(null);
                handle.DrawTextureRect(item.GetFrame(RsiDirection.South, 0),
                    MeleeSwingVisuals.TextureBounds(weapon.RSI.Size, style.WeaponPivotPixels, 32, style.WeaponScale),
                    Color.White.WithAlpha(alpha));
                handle.UseShader(_shader);
            }
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
