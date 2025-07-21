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

        private static readonly Color RedFlash = new(1f, 0f, 0f, 0.8f);
        private static readonly SoundSpecifier GibSound = new SoundCollectionSpecifier("gib"); // or use new SoundPathSpecifier("/Audio/Effects/MeatLaserImpact.ogg")

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
            _audio.PlayPvs(GibSound, xform.Coordinates);

            // Spawn Ash at the mob's location
            EntityManager.SpawnEntity("Ash", xform.Coordinates);
            EntityManager.QueueDeleteEntity(other);

            // Уменьшить таймер всплеска у кристалла
            if (EntityManager.TryGetComponent<SupermatterEventComponent>(uid, out var events))
            {
                events.NextEventTimer = 0f;
                events.ForceEvent = true;
            }
        }
    }
}
