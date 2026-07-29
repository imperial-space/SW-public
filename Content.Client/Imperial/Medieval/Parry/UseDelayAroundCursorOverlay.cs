using System;
using System.Numerics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.UseDelayAroundCursor;

public sealed class UseDelayAroundCursorOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly SharedHandsSystem _hands;
    private readonly UseDelaySystem _useDelay;

    public UseDelayAroundCursorOverlay(
        IGameTiming timing,
        IInputManager input,
        IEntityManager entMan,
        IPlayerManager player,
        SharedHandsSystem hands,
        UseDelaySystem useDelay)
    {
        _timing = timing;
        _input = input;
        _entMan = entMan;
        _player = player;
        _hands = hands;
        _useDelay = useDelay;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var playerEnt = _player.LocalEntity;
        if (playerEnt == null)
            return;

        var targetEnt = EntityUid.Invalid;

        if (_entMan.HasComponent<UseDelayAroundCursorComponent>(playerEnt.Value))
        {
            targetEnt = playerEnt.Value;
        }
        else if (_hands.TryGetActiveItem(playerEnt.Value, out var activeItem) &&
                 _entMan.HasComponent<UseDelayAroundCursorComponent>(activeItem.Value))
        {
            targetEnt = activeItem.Value;
        }

        if (targetEnt == EntityUid.Invalid)
            return;

        if (!_entMan.TryGetComponent<UseDelayComponent>(targetEnt, out var useDelayComp))
            return;

        var delay = _useDelay.GetLastEndingDelay((targetEnt, useDelayComp));
        var now = _timing.CurTime;

        if (now >= delay.EndTime || now < delay.StartTime)
            return;

        var duration = (delay.EndTime - delay.StartTime).TotalSeconds;
        if (duration <= 0)
            return;

        var elapsed = (now - delay.StartTime).TotalSeconds;
        var progress = Math.Clamp(elapsed / duration, 0.0, 1.0);

        var handle = args.ScreenHandle;
        var center = _input.MouseScreenPosition.Position;

        var innerR = 25f;
        var outerR = 30f;
        var segments = 48;

        float fullCircle = MathF.PI * 2f;
        float startAngle = -MathF.PI / 2f + (fullCircle * (float)progress);
        float sweepAngle = fullCircle * (1.0f - (float)progress);

        DrawPie(handle, center, innerR, outerR, startAngle, sweepAngle, segments);
    }

    private void DrawPie(DrawingHandleScreen handle, Vector2 center, float innerRadius, float outerRadius, float startAngle, float sweepAngle, int segments)
    {
        if (sweepAngle <= 0) return;

        var vertices = new Vector2[(segments + 1) * 2];

        for (var i = 0; i <= segments; i++)
        {
            var angle = startAngle + (sweepAngle * (i / (float)segments));
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            vertices[i * 2] = center + direction * outerRadius;
            vertices[i * 2 + 1] = center + direction * innerRadius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, vertices, Color.White.WithAlpha(0.6f));
    }
}
