using Content.Shared.Imperial.Medieval.CameraReset;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.CameraReset;

public sealed class ResetCameraOnGridEnterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private EntityUid? _pendingReset;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ResetCameraOnGridEnterEvent>(OnResetCamera);
    }

    private void OnResetCamera(ResetCameraOnGridEnterEvent args)
    {
        var entity = GetEntity(args.Entity);
        if (_player.LocalEntity == entity)
            _pendingReset = entity;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingReset is not { } entity ||
            !_timing.InPrediction ||
            !_timing.IsFirstTimePredicted)
        {
            return;
        }

        _pendingReset = null;

        if (_player.LocalEntity == entity)
            RaisePredictiveEvent(new RequestResetCameraOnGridEnterEvent());
    }
}
