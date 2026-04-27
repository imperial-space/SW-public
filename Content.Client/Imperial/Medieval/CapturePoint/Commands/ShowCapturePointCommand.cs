using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client.Imperial.Medieval.CapturePoint.Commands;

public sealed class ShowCapturePointCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "showcapturepoint";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayMan.HasOverlay<CapturePointDebugOverlay>())
        {
            _overlayMan.RemoveOverlay<CapturePointDebugOverlay>();
        }
        else
        {
            _overlayMan.AddOverlay(new CapturePointDebugOverlay(_entMan, _transform));
        }
    }
}
