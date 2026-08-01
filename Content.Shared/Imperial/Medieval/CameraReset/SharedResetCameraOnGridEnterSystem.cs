using Content.Shared.Movement.Systems;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.CameraReset;

public sealed class SharedResetCameraOnGridEnterSystem : EntitySystem
{
    [Dependency] private readonly SharedMoverController _mover = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<RequestResetCameraOnGridEnterEvent>(OnResetCamera);
    }

    private void OnResetCamera(RequestResetCameraOnGridEnterEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is { } entity)
            _mover.ResetCamera(entity);
    }
}
