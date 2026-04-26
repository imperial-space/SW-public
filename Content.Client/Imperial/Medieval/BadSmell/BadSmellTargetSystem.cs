using Content.Shared.Ratling;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.Ratling;

public sealed class BadSmellTargetSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private BadSmellTargetOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var localPlayer = _playerManager.LocalEntity;

        if (localPlayer != null && HasComp<BadSmellVisionComponent>(localPlayer))
        {
            if (!_overlayManager.HasOverlay<BadSmellTargetOverlay>())
                _overlayManager.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayManager.HasOverlay<BadSmellTargetOverlay>())
                _overlayManager.RemoveOverlay<BadSmellTargetOverlay>();
        }
    }
}
