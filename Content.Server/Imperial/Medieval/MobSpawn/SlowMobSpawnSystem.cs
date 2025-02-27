using Content.Server.SlowMobSpawn.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using System.Linq;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Spawners;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Server.Storage.Components;
using Content.Shared.Religion.Components;

namespace Content.Server.MagicPotionsMaker
{
    public sealed partial class SlowMobSpawnSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SlowMobSpawnComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<EntityStorageComponent, TimedDespawnEvent>(OnDespawn);

        }

        public void OnDespawn(EntityUid uid, EntityStorageComponent component, TimedDespawnEvent args)
        {
            if (!CheckHumanNearby(uid))
                _storage.TryCloseStorage(uid);
        }
        public bool CheckHumanNearby(EntityUid uid)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 1.5f))
            {
                if (TryComp<ReligionMemberComponent>(entity, out var religion))
                    return true;
            }
            return false;
        }

        public void OnStartup(EntityUid uid, SlowMobSpawnComponent component, ComponentStartup args)
        {
            if (!component.Enabled)
                return;
            var xform = Transform(component.Owner);
            var coords = xform.Coordinates;
            component.Effect = Spawn(component.SpawnEffect, coords);
            Audio.PlayPvs(new SoundPathSpecifier(component.SoundEffect), uid, AudioParams.Default.WithVariation(0.15f));
            component.StartTime = _timing.CurTime;
            component.EndTime = component.StartTime + component.ReloadTime;
            component.Active = true;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<SlowMobSpawnComponent>())
            {

                if (_timing.CurTime > comp.EndTime && comp.Active)
                {
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;
                    if (comp.Effect != null)
                        QueueDel(comp.Effect);
                    Spawn(comp.SpawnMob, coords);
                    comp.Active = false;

                }

            }
        }

    }
}
