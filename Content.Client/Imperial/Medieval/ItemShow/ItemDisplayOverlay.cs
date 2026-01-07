using System.Numerics;
using Content.Shared.Imperial.Medieval.ItemShow;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;


namespace Content.Client.Imperial.Medieval.ItemShow;

public sealed class ItemDisplayOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly EntityLookupSystem _entityLookupSystem;
    private readonly SpriteSystem _spriteSystem;


    private ShaderInstance _shader;
    private Texture _boubleTexture;

    public ItemDisplayOverlay()
    {
        IoCManager.InjectDependencies(this);

        _spriteSystem = _entityManager.System<SpriteSystem>();
        _entityLookupSystem = _entityManager.System<EntityLookupSystem>();

        _shader = _prototypeManager.Index<ShaderPrototype>("unshaded").Instance();

        var textureSpecifier = new SpriteSpecifier.Texture(new ResPath("/Textures/Imperial/Medieval/Interface/ItemDisplay/bouble.png"));
        _boubleTexture = _spriteSystem.Frame0(textureSpecifier);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localPlayer = _playerManager.LocalEntity;

        if (!localPlayer.HasValue)
            return;

        var query = _entityManager.EntityQueryEnumerator<ItemDisplayComponent, TransformComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var itemShowComponent, out var xformComponent, out var spriteComponent))
        {
            if (xformComponent.MapID != args.MapId || !spriteComponent.Visible)
                continue;

            if (xformComponent.MapID != args.MapId ||
                    !spriteComponent.Visible || !_entityManager.TryGetComponent<SpriteComponent>(itemShowComponent.ItemUid, out var itemSprite))
                continue;

            var currentZoom = _eyeManager.CurrentEye.Scale;
            var aabb = _entityLookupSystem.GetWorldAABB(uid);
            var worldCenter = aabb.Center;

            var screenPos = _eyeManager.WorldToScreen(worldCenter +
                                                      new Angle(-_eyeManager.CurrentEye.Rotation)
                                                          .RotateVec(aabb.TopRight - aabb.Center));
            var spriteWorldBound = _spriteSystem.GetLocalBounds((uid, spriteComponent));
            var spriteWorldSize = spriteWorldBound.Size;
            var finalScale = 0.7f * currentZoom;
            // 🌫 ТУДУ: сделать нормальный скейлинг что бы мелкие обьекты были нормально видны в облачке
            var boubleOffset = new Vector2(10, -21) * currentZoom;
            var adjustedScreenPos = screenPos + boubleOffset;

            var boubleSize = _boubleTexture.Size * 2.8f * currentZoom;
            var boubleRect = UIBox2.FromDimensions(adjustedScreenPos - (boubleSize / 2f), boubleSize);

            var itemOffset = new Vector2(13, -19) * currentZoom;
            var itemDrawPos = screenPos + itemOffset;

            args.ScreenHandle.UseShader(null);
            args.ScreenHandle.DrawTextureRect(_boubleTexture, boubleRect);
            args.ScreenHandle.DrawEntity(itemShowComponent.ItemUid, itemDrawPos, finalScale, Angle.Zero, Angle.Zero, Direction.South);

        }
    }
}
