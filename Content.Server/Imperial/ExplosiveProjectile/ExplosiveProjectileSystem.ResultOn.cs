using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Body.Systems;
using Content.Server.Imperial.ExplosiveProjectile.Components;

namespace Content.Server.Imperial.ExplosiveProjectile
{
    [UsedImplicitly]
    public sealed class ExplosiveProjectileResultOnSystem : EntitySystem
    {
        [Dependency] private readonly SharedBodySystem _body = default!;
        [Dependency] private readonly ExplosionSystem _explosion = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
        private void GibEntity(EntityUid uid)
        {
            _explosion.QueueExplosion(uid, ExplosionSystem.DefaultExplosionPrototypeId, 1, 1, 1);
            _body.GibBody(uid, splatModifier: 5f);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ExplosiveProjectileResultOnComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                var frameTimeTS = TimeSpan.FromSeconds(frameTime);
                comp.DTime -= frameTimeTS;
                var dTime = comp.DTime;
                var endTime = TimeSpan.FromSeconds(0);
                if (endTime > dTime)
                    GibEntity(uid);
            }
        }
    }
}
