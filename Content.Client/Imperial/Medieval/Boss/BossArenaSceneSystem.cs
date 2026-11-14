using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Graphics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Enums;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossArenaSceneSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public static Angle ProfileRotation(Angle onScreen)
        => BossArenaWarningVisuals.ProfileRotation(onScreen);

    public static void ApplyPose(SpriteSystem sprites, Entity<SpriteComponent> sprite, Angle onScreen)
    {
        var cardinal = ProfileRotation(onScreen);
        sprites.SetRotation((sprite.Owner, sprite.Comp), -cardinal);

        var index = 0;
        foreach (var _ in sprite.Comp.AllLayers)
            sprites.LayerSetRotation((sprite.Owner, sprite.Comp), index++, cardinal);
    }

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new SceneViewOverlay(this));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<SceneViewOverlay>();
        base.Shutdown();
    }

    public void ApplyView(IEye? eye)
    {
        if (eye == null)
            return;
        var query = EntityQueryEnumerator<BossArenaSceneComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var transform))
            if (transform.MapID == eye.Position.MapId)
                ApplyPose(_sprites, (uid, sprite), _transform.GetWorldRotation(transform) + eye.Rotation);
    }

    private sealed class SceneViewOverlay(BossArenaSceneSystem scene) : Overlay
    {

        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

        protected override void Draw(in OverlayDrawArgs args)
            => scene.ApplyView(args.Viewport.Eye);
    }
}
