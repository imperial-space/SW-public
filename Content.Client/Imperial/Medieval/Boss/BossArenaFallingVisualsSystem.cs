using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossArenaFallingVisualsSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly string _chasmFallAnimationKey = "boss_arena_fall";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BossArenaFallVisualComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BossArenaFallVisualComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, BossArenaFallVisualComponent component, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            TerminatingOrDeleted(uid))
        {
            return;
        }

        component.OriginalScale = sprite.Scale;

        if (!TryComp<AnimationPlayerComponent>(uid, out var player))
            return;

        if (_anim.HasRunningAnimation(player, _chasmFallAnimationKey))
            return;

        _anim.Play((uid, player), GetFallingAnimation(component), _chasmFallAnimationKey);
    }

    private void OnComponentRemove(EntityUid uid, BossArenaFallVisualComponent component, ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetScale((uid, sprite), component.OriginalScale);

        if (!TryComp<AnimationPlayerComponent>(uid, out var player))
            return;

        if (_anim.HasRunningAnimation(player, _chasmFallAnimationKey))
            _anim.Stop((uid, player), _chasmFallAnimationKey);
    }

    private Animation GetFallingAnimation(BossArenaFallVisualComponent component)
    {
        var length = component.AnimationTime;

        return new Animation()
        {
            Length = length,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(component.OriginalScale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(component.AnimationScale, length.Seconds),
                    },
                    InterpolationMode = AnimationInterpolationMode.Cubic
                }
            }
        };
    }
}
