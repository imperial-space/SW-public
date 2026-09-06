using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Imperial.Medieval.NoShootOnUseDelay;

public sealed partial class SharedMedievalMagicSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _useDelaySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoShootOnUseDelayComponent, ShotAttemptedEvent>(OnShoot);
    }

    private void OnShoot(Entity<NoShootOnUseDelayComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_useDelaySystem.IsDelayed(args.Used.Owner))
            args.Cancel();
    }
}
