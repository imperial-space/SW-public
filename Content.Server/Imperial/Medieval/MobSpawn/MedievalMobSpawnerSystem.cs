using Content.Server.MedievalMobSpawner.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map.Components;

namespace Content.Server.MedievalMobSpawner
{
    public sealed partial class MedievalMobSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private readonly HashSet<EntityUid> _activeSpawners = new(); // EntitySet для спаунеров

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MedievalMobSpawnerComponent, ComponentStartup>(OnSpawnerStartup);
            SubscribeLocalEvent<MedievalMobSpawnerComponent, ComponentShutdown>(OnSpawnerShutdown);

            // Подписываемся на ProximityEvent, когда триггер входит в зону спаунера
            SubscribeLocalEvent<MedievalMobTriggerComponent, StartCollideEvent>(OnTriggerEnter);

        }

        private void OnSpawnerStartup(EntityUid uid, MedievalMobSpawnerComponent component, ComponentStartup args)
        {
            _activeSpawners.Add(uid); // Добавляем в EntitySet
        }

        private void OnSpawnerShutdown(EntityUid uid, MedievalMobSpawnerComponent component, ComponentShutdown args)
        {
            _activeSpawners.Remove(uid); // Удаляем из EntitySet
        }

        // Обработчик события ProximityEvent
        private void OnTriggerEnter(EntityUid uid, MedievalMobTriggerComponent component, StartCollideEvent args)
        {
            if (args.OurFixtureId != component.FixtureId)
                return;
            if (!TryComp<MedievalMobSpawnerComponent>(args.OtherEntity, out var spawner))
                return; // Не спаунер

            if (spawner.Ready)
            {
                TrySpawnMob(spawner);
            }
        }


        private void TrySpawnMob(MedievalMobSpawnerComponent spawner)
        {
            spawner.Ready = false;
            spawner.Cooldown = spawner.MaxCoolDown;

            float currentChance = spawner.Chance;
            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                float halfChance = currentChance / 2;
                float unstability = (barrier.MaxStability - barrier.Stability) / barrier.MaxStability;
                currentChance = halfChance + unstability * halfChance;
            }

            if (_random.Prob(currentChance))
            {
                var spawnerxform = Transform(spawner.Owner);
                var spawnercoords = spawnerxform.Coordinates;
                Spawn(spawner.SpawnedEntity, spawnercoords);
            }
        }


        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var curTime = _timing.CurTime;

            // Обновляем кулдаун спаунеров (теперь только это делаем в Update)
            foreach (var uid in _activeSpawners)
            {
                if (!TryComp<MedievalMobSpawnerComponent>(uid, out var spawner))
                {
                    continue; // Компонент мог быть удален
                }

                if (spawner.Cooldown <= 0)
                {
                    spawner.Ready = true;
                }
                else
                {
                    spawner.Cooldown -= frameTime;
                }
            }

        }

    }
}
