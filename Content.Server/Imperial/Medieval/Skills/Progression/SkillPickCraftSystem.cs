using Content.Shared.DoAfter;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Content.Shared.Verbs;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

public sealed class SkillPickCraftSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    private static readonly HashSet<string> Materials = new() { "Fork", "Spoon", "MaterialBones", "MaterialBones1", "MedievalIronPlateSmall", "MedievalSkillStick" };

    public override void Initialize()
    {
        SubscribeLocalEvent<ItemComponent, GetVerbsEvent<AlternativeVerb>>(OnVerb);
        SubscribeLocalEvent<SkillsComponent, SkillPickCraftEvent>(OnCraft);
    }

    private bool CanCraft(EntityUid user, EntityUid material) => !TerminatingOrDeleted(material)
        && !EntityManager.IsQueuedForDeletion(material)
        && TryComp<SkillsComponent>(user, out var skills) && SkillScaling.Level(skills, SharedSkillsSystem.AgilityId) >= SkillScaling.Master
        && Materials.Contains(MetaData(material).EntityPrototype?.ID ?? "") && _blocker.CanInteract(user, material)
        && _interaction.InRangeUnobstructed(user, material);

    private void OnVerb(EntityUid uid, ItemComponent item, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (CanCraft(args.User, uid))
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("skills-craft-lockpick"),
                Act = () =>
                {
                    if (CanCraft(args.User, uid))
                        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 3f, new SkillPickCraftEvent(), args.User, uid, uid)
                        {
                            NeedHand = true, BreakOnMove = true, BreakOnDamage = true,
                            AllowMovementAssistance = true, DistanceThreshold = 2f
                        });
                }
            });
        // A plain wooden stick can be cut from one existing wooden material unit.
        if (MetaData(uid).EntityPrototype?.ID is "MaterialWoodPlank" or "MaterialWoodPlank1" or "MaterialWoodPlank10")
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("skills-craft-stick"),
                Act = () =>
                {
                    if (!TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) && _blocker.CanInteract(args.User, uid)
                        && _interaction.InRangeUnobstructed(args.User, uid) && _stacks.Use(uid, 1))
                        _hands.PickupOrDrop(args.User, Spawn("MedievalSkillStick", Transform(args.User).Coordinates));
                }
            });
    }

    private void OnCraft(EntityUid uid, SkillsComponent skills, SkillPickCraftEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } material || !CanCraft(uid, material))
            return;
        if (TryComp<StackComponent>(material, out var stack))
        {
            if (!_stacks.Use(material, 1, stack))
                return;
        }
        else
            QueueDel(material);
        args.Handled = true;
        _hands.PickupOrDrop(uid, Spawn("MedievalImprovisedLockpick", Transform(uid).Coordinates));
    }
}
