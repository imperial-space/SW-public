using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
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
        [Dependency] private readonly IGameTiming _timing = default!;

        private TimeSpan _delayTime;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ExplosiveProjectileResultOffComponent, ComponentStartup>(GetDelayTime);
        }
        private void DoNotGibEntity(EntityUid uid, ExplosiveProjectileResultOffComponent component)
        {
            _audio.PlayPvs(component.SoundDeactivate, uid);
            RemComp<ExplosiveProjectileResultOffComponent>(uid);
        }
        private void GetDelayTime(EntityUid uid, ExplosiveProjectileResultOffComponent component, ComponentStartup args)
        {
            _delayTime = _timing.CurTime + component.CancelTime;
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ExplosiveProjectileResultOffComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.CurrentTime < _delayTime)
                    comp.CurrentTime = _timing.CurTime;
                else
                    DoNotGibEntity(uid, comp);
            }
        }
    }
}
