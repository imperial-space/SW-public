using Content.Shared.Movement.Events;

namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFallingSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<BossArenaFallVisualComponent, UpdateCanMoveEvent>(OnCanMove);

    private void OnCanMove(EntityUid uid, BossArenaFallVisualComponent component, UpdateCanMoveEvent args)
        => args.Cancel();
}
