using Content.Server.Imperial.Power.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Events;
using Content.Server.Effects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Effects;
using System.Numerics;

namespace Content.Server.Imperial.Power.EntitySystems
{
    public sealed class SupermatterTouchSystem : EntitySystem
    {
        [Dependency] private readonly ColorFlashEffectSystem _colorFlash = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

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

            // Play gib sound at the mob's location
            var xform = Transform(other);
            _audio.PlayPvs(component.GibSound, xform.Coordinates);

            // Spawn Ash at the mob's location
            EntityManager.SpawnEntity(component.AshPrototype, xform.Coordinates);
            EntityManager.QueueDeleteEntity(other);

            // Вспышка цвета из компонента
            _colorFlash.RaiseEffect(component.FlashColor, new List<EntityUid> { other }, Filter.Pvs(other));

            // Публикуем ивент о касании суперматерии
            var ev = new SupermatterTouchedEvent();
            RaiseLocalEvent(uid, ev);
        }
    }

    public sealed class SupermatterTouchedEvent : EntityEventArgs {}
}
