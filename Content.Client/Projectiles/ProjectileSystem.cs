using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
// Imperial medieval start
using Content.Shared.Sound.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
// Imperial medieval end
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!; // Imperial medieval edit

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<ImpactEffectEvent>(OnProjectileImpact); // Imperial medieval edit - reuse renderer for local feedback.
    }

    private void OnProjectileImpact(ImpactEffectEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);

        if (Deleted(coords.EntityId))
            return;

        var ent = Spawn(ev.Prototype, coords);

        // Imperial medieval start - effects spawned by this client renderer do not run server EmitSoundOnSpawn.
        if (TryComp<EmitSoundOnSpawnComponent>(ent, out var sound))
        {
            if (sound.Positional)
                _audio.PlayStatic(sound.Sound, Filter.Local(), coords, true);
            else
                _audio.PlayEntity(sound.Sound, Filter.Local(), ent, true);
        }
        // Imperial medieval end

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerMapTryGet((ent, sprite), EffectLayers.Unshaded, out var layer, false);
            var state = _sprite.LayerGetRsiState((ent, sprite), layer);
            var lifetime = 0.5f;

            if (TryComp<TimedDespawnComponent>(ent, out var despawn))
                lifetime = despawn.Lifetime;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                        }
                    }
                }
            };

            _player.Play(ent, anim, "impact-effect");
        }
    }
}
