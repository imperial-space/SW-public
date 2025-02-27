using Content.Server.MedievalJobSpawn.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using System.Linq;
using Robust.Server.GameObjects;

namespace Content.Server.MagicPotionsMaker
{
    public sealed partial class MedievalJobSpawnSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MedievalJobSpawnComponent, ComponentStartup>(OnStartup);

        }


        public void OnStartup(EntityUid uid, MedievalJobSpawnComponent component, ComponentStartup args)
        {
            if (!component.Enabled)
                return;
            var spawns = EntityManager.EntityQuery<MedievalJobPointComponent>().ToList();
            _random.Shuffle(spawns);
            foreach (var spawn in spawns)
            {
                if (spawn.SpawnType == component.SpawnType)
                {
                    if (!TryComp<TransformComponent>(spawn.Owner, out var pointTransform) || !spawn.Enabled) continue;
                    _transformSystem.SetCoordinates(uid, pointTransform.Coordinates);
                    _transformSystem.AttachToGridOrMap(uid);
                }

            }
        }

    }
}
