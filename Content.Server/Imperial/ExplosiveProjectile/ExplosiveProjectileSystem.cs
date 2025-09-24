using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Content.Server.Stunnable;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Server.Imperial.ExplosiveProjectile.Components;

namespace Content.Server.Imperial.ExplosiveProjectile
{
    [UsedImplicitly]
    internal sealed class ExplosiveProjectileSystem : EntitySystem
    {
        [Dependency] private readonly StunSystem _stunSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedBodySystem _body = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        private bool _isOnExplodeStartWas = false;
        private bool _checkTime = false;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ExplosiveProjectileComponent, StartCollideEvent>(HandleCollide);
        }
        private void TryExplodeEntity(EntityUid uid, ExplosiveProjectileComponent component, EntityUid target) //float dTime
        {
            if (!_checkTime)
                return;

            if (EntityManager.HasComponent<BodyComponent>(target))
            {
                _body.GibBody(target, splatModifier: 5f);
            }
            else
            {
                OnExplodeFailed(uid, component, target);
            }
        }
        private void OnExplodeFailed(EntityUid uid, ExplosiveProjectileComponent component, EntityUid target)
        {
            Audio.PlayPvs(component.SoundDeactivate, target);
            return;
        }
        private void OnExplodeStart(EntityUid uid, ExplosiveProjectileComponent component, EntityUid target)
        {
            Audio.PlayPvs(component.SoundActivate, target);
            if (EntityManager.TryGetComponent<StatusEffectsComponent>(target, out var status))
            {
                _stunSystem.TryStun(target, TimeSpan.FromSeconds(component.StunAmount), true, status);
                _stunSystem.TryKnockdown(target, TimeSpan.FromSeconds(component.KnockdownTime), true, status);
                _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(component.SlowdownParam), true, component.WalkSpeedParam, component.RunSpeedParam, status);
            }
            _isOnExplodeStartWas = true;
            TryExplodeEntity(uid, component, target);
        }
        private void HandleCollide(EntityUid uid, ExplosiveProjectileComponent component, ref StartCollideEvent args)
        {
            if (args.OurFixtureId != component.CheckedFixtureID)
                return;

            OnExplodeStart(uid, component, args.OtherEntity);
        }

        #region Ебучий фреймапдейт

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<ExplosiveProjectileComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (_isOnExplodeStartWas)
                    continue;
                if (_timing.CurTime >= comp.DetonationTimeFin)
                    comp.DetonationTimeFin = comp.DetonationTime + _timing.CurTime;
                continue;
            }
            _checkTime = true;
        }
        #endregion
    }
}
