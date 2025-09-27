using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Content.Shared.Standing;
using Content.Server.Imperial.ExplosiveProjectile.Components;

namespace Content.Server.Imperial.ExplosiveProjectile
{
    [UsedImplicitly]
    internal sealed class ExplosiveProjectileResultOffSystem : EntitySystem
    {
        [Dependency] protected readonly SharedAudioSystem Audio = default!;

        public override void Initialize()
        {
            base.Initialize();
            UpdatesOutsidePrediction = true;
        }
        private void DoNotGibEntity(EntityUid uid, ExplosiveProjectileResultOffComponent component)
        {
            Audio.PlayPvs(component.SoundDeactivateA, uid);
            EntityManager.RemoveComponent<ExplosiveProjectileResultOffComponent>(uid);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ExplosiveProjectileResultOffComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                comp.CTime -= frameTime;
                var cTime = TimeSpan.FromSeconds(comp.CTime);
                var endTime = TimeSpan.FromSeconds(0);
                if (endTime >= cTime)
                    DoNotGibEntity(uid, comp);
            }
        }
    }
}
