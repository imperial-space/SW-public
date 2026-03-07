using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Interaction;
using Content.Shared.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;


namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

/// <summary>
/// система для поднятия и опускания якоря
/// </summary>
public sealed class MedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<MedievalAnchorComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, MedievalAnchorComponent component, InteractUsingEvent args)
    {
        Use(args.User, args.Target);
    }

    private void OnActivate(EntityUid uid, MedievalAnchorComponent component, ActivateInWorldEvent args)
    {
        Use(args.User, args.Target);
    }

    private void Use(EntityUid playerEntity, EntityUid target)
    {


         var time = 7 -_skills.GetSkillLevel(playerEntity, "Strength") * 0.3f;
         var sdoAfter = new DoAfterArgs(EntityManager,
             playerEntity,
             time,
             new UseAnchorEvent(),
             target,
             target: playerEntity)
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


         _doAfter.TryStartDoAfter(sdoAfter);

    }
}
