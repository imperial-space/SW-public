using Content.Server.Melter.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Melter
{
    public sealed partial class MeltSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        TimeSpan ReloadTime = TimeSpan.FromSeconds(40f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + ReloadTime;

                foreach (var comp in EntityManager.EntityQuery<MelterComponent>())
                {
                    var uid = comp.Owner;
                    var xform = Transform(uid);
                    var coords = xform.Coordinates;
                    foreach (var target in _lookup.GetEntitiesInRange(coords, 0.3f))
                    {
                        if (TryComp<MeltableComponent>(target, out var meltable))
                        {
                            if (meltable.Enabled)
                            {
                                meltable.MeltLevel++;
                                if (meltable.MeltLevel >= meltable.MaxMeltLevel)
                                {
                                    while (meltable.ResourceCount > 0)
                                    {
                                        meltable.ResourceCount--;
                                        Spawn(meltable.ResourceName, coords);
                                    }
                                    QueueDel(meltable.Owner);
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}
