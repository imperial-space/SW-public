using System.Numerics;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Medieval.CapturePoint;

public sealed class CapturePointDebugOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;

    public CapturePointDebugOverlay(
        IEntityManager entManager,
        SharedTransformSystem transform)
    {
        _entManager = entManager;
        _transform = transform;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        
        var query = _entManager.AllEntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            if (xform.MapID != args.Viewport.Eye?.Position.MapId)
                continue;

            var (pos, rot) = _transform.GetWorldPositionRotation(uid);
            var half = point.Radius;
            var right = rot.ToVec();
            var up = new Vector2(-right.Y, right.X);

            var tl = pos + (-right + up) * half;
            var tr = pos + ( right + up) * half;
            var br = pos + ( right - up) * half;
            var bl = pos + (-right - up) * half;

            handle.DrawLine(tl, tr, Color.Yellow);
            handle.DrawLine(tr, br, Color.Yellow);
            handle.DrawLine(br, bl, Color.Yellow);
            handle.DrawLine(bl, tl, Color.Yellow);
        }
    }
}