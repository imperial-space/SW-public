using System;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

public sealed class MedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MedievalAnchorComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, MedievalAnchorComponent component, InteractUsingEvent args)
    {
        Use(args.User, args.Target, component);
    }

    private void OnActivate(EntityUid uid, MedievalAnchorComponent component, ActivateInWorldEvent args)
    {
        Use(args.User, args.Target, component);
    }

    private void Use(EntityUid playerEntity, EntityUid target, MedievalAnchorComponent component)
    {
        if (!_skills.HasSkill(playerEntity, SharedSkillsSystem.StrengthId))
            return;

        var time = component.BaseUseTime - _skills.GetSkillLevel(playerEntity, "Strength") * component.StrengthUseTimeModifier;
        time = Math.Max(1.0f, time);

        var doAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new UseAnchorEvent(),
            target,
            target: target)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }
}
