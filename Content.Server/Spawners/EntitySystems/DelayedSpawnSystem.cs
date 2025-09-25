using Content.Server.Spawners.Components;
using Robust.Shared.Timing;
namespace Content.Server.Spawners.EntitySystems;

public sealed class DelayedSpawnSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_timing.IsFirstTimePredicted)
            return;
        var query = AllEntityQuery<DelayedSpawnComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ParentSpawnTime == TimeSpan.FromSeconds(0))
                comp.ParentSpawnTime = _timing.CurTime;
            if (!(comp.ParentSpawnTime + TimeSpan.FromSeconds(comp.Delay) < _timing.CurTime && !comp.IsSpawned))
                continue;
            var entity = Spawn(comp.Proto, Transform(uid).Coordinates);
            if (comp.Attached)
                _transform.SetParent(entity, uid);
            comp.IsSpawned = true;
        }
    }
}
