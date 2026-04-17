using System.Numerics;
using Content.Shared.Ratling;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client.Ratling;

public sealed class BadSmellTargetOverlay : Overlay
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => false;

    private readonly Color _glowColor = new(0.2f, 0.8f, 0.2f, 0.3f); // Зелёный, прозрачный
    private readonly float _glowRadius = 0.05f; // Радиус кружка

    public BadSmellTargetOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localPlayer = _playerManager.LocalEntity;

        if (localPlayer == null)
            return;

        if (!_entityManager.TryGetComponent<BadSmellVisionComponent>(localPlayer, out var vision))
            return;

        var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        var worldHandle = args.WorldHandle;
        var localPos = xformQuery.GetComponent(localPlayer.Value).WorldPosition;

        foreach (var comp in _entityManager.EntityQuery<BadSmellMarkerComponent>(true))
        {
            var uid = comp.Owner;
            if (!xformQuery.TryGetComponent(uid, out var xform))
                continue;
            if (xform.MapID != eye.Position.MapId)
                continue;
            if ((localPos - xform.WorldPosition).Length() > vision.Radius)
                continue;
            if (uid == localPlayer)
                continue;
            var worldPos = xform.WorldPosition;

            worldHandle.DrawCircle(worldPos, _glowRadius, _glowColor);
        }
    }
}
