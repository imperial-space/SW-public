using Content.Server.MedievalSelfHeal.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using System.Linq;
using Robust.Server.GameObjects;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Magic.Mana;

namespace Content.Server.MagicPotionsMaker
{
    public sealed partial class MedievalSelfHealSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly DamageableSystem _damage = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MedievalSelfHealComponent, ComponentStartup>(OnStartup);

        }


        public void OnStartup(EntityUid uid, MedievalSelfHealComponent comp, ComponentStartup args)
        {
            var position = Transform(uid).Coordinates;
            var humans = _lookup.GetEntitiesInRange<ManaComponent>(position, 1.1f);

            foreach (var human in humans)
            {
                _damage.TryChangeDamage(human.Owner, -comp.HealDamage, true, false);

            }
            QueueDel(uid);
        }

    }
}
