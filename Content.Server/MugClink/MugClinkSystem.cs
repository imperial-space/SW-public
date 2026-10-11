using Content.Shared.Audio;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.MugClink;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.MugClink;

public sealed class MugClinkSystem : EntitySystem
{
    private const float ClinkRange = 1f;
    private static readonly TimeSpan ClinkCooldown = TimeSpan.FromSeconds(2);
    private static readonly SoundSpecifier MugClinkSound = new SoundPathSpecifier("/Audio/Items/mug_clink.ogg");

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MugClinkableComponent, AfterInteractEvent>(OnAfterInteract,
            before: new[] { typeof(IngestionSystem) });
    }

    private void OnAfterInteract(EntityUid uid, MugClinkableComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!args.CanReach ||
            args.User == target ||
            !_hands.IsHolding(args.User, uid) ||
            !TryComp<MindContainerComponent>(args.User, out var userMind) ||
            !userMind.HasMind ||
            !TryComp<MindContainerComponent>(target, out var targetMind) ||
            !targetMind.HasMind ||
            !TryComp<HandsComponent>(target, out var targetHands))
            return;

        var targetHasMug = false;
        foreach (var held in _hands.EnumerateHeld((target, targetHands)))
        {
            if (!HasComp<MugClinkableComponent>(held))
                continue;

            targetHasMug = true;
            break;
        }

        if (!targetHasMug)
            return;

        args.Handled = true;
        if (!_transform.InRange(Transform(args.User).Coordinates, Transform(target).Coordinates, ClinkRange))
            return;

        var clinkCount = EnsureComp<MugClinkCountComponent>(args.User);
        if (_timing.CurTime < clinkCount.NextClinkTime)
            return;

        clinkCount.NextClinkTime = _timing.CurTime + ClinkCooldown;
        clinkCount.Count++;
        _audio.PlayPvs(MugClinkSound, target);
    }
}
