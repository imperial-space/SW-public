using Content.Server.Imperial.Power.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;
using Content.Server.Effects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterTouchSystem : EntitySystem
    {
        [Dependency] private readonly ColorFlashEffectSystem _colorFlash = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SupermatterTouchComponent, StartCollideEvent>(OnStartCollide);
        }

        private void OnStartCollide(EntityUid uid, SupermatterTouchComponent component, ref StartCollideEvent args)
        {
            var other = args.OtherEntity;
            if (!EntityManager.HasComponent<MobStateComponent>(other))
                return;

            var transformComp = EntityManager.GetComponentOrNull<TransformComponent>(other);
            if (transformComp == null)
                return;

            PlayGibSound(component, transformComp);
            ShowColorFlash(component, other);
            SpawnAshAndDelete(component, transformComp, other);
            RaiseLocalEvent(uid, new SupermatterTouchedEvent());
        }

        private void PlayGibSound(SupermatterTouchComponent component, TransformComponent transformComp)
        {
            _audio.PlayPvs(component.GibSound, transformComp.Coordinates);
        }

        private void ShowColorFlash(SupermatterTouchComponent component, EntityUid other)
        {
            _colorFlash.RaiseEffect(component.FlashColor, [other], Filter.Pvs(other));
        }

        private void SpawnAshAndDelete(SupermatterTouchComponent component, TransformComponent transformComp, EntityUid other)
        {
            EntityManager.SpawnEntity(component.AshPrototype, transformComp.Coordinates);
            EntityManager.QueueDeleteEntity(other);
        }
    }

    public sealed class SupermatterTouchedEvent : EntityEventArgs;
}
