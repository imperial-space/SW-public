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
    public sealed class ExplosiveProjectileResultOffSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
        private void DoNotGibEntity(EntityUid uid, ExplosiveProjectileResultOffComponent component)
        {
            _audio.PlayPvs(component.SoundDeactivate, uid);
            RemComp<ExplosiveProjectileResultOffComponent>(uid);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ExplosiveProjectileResultOffComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                var frameTimeTS = TimeSpan.FromSeconds(frameTime);
                comp.CTime -= frameTimeTS;
                var cTime = comp.CTime;
                var endTime = TimeSpan.FromSeconds(0);
                if (endTime >= cTime)
                    DoNotGibEntity(uid, comp);
            }
        }
    }
}
