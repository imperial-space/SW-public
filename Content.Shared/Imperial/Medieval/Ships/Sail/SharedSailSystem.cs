using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Follower;
using Content.Shared.Imperial.Medieval.Ships.Oar;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Ships.Sail;

/// <summary>
/// вращяет парус по ветру
/// </summary>
public sealed class SharedSailSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<SailComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<SailComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SailComponent, SailUseEvent>(OnUse);
    }

    private void OnActivate(EntityUid uid, SailComponent component, ActivateInWorldEvent args)
    {
        UseDelay(args.User, args.Target);
    }

    private void OnInteractUsing(EntityUid uid, SailComponent component, InteractUsingEvent args)
    {
        UseDelay(args.User, args.Target);
    }

    private void UseDelay(EntityUid playerEntity, EntityUid targetEntity)
    {

        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.3f;
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new SailUseEvent(),
            targetEntity,
            playerEntity)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(sdoAfter);
    }

    private void OnUse(EntityUid uid, SailComponent component, SailUseEvent args)
    {
        if (args.Cancelled)
            return;

        Rotate(uid);
    }

    private void Rotate(EntityUid uid)
    {
        _transform.SetWorldRotation(uid, _cfg.GetCVar(ShipsCCVars.WindRotation)/180);
    }

    private void OnGetAlternativeVerbs(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (ev.User == ev.Target || IsClientSide(ev.Target))
            return;

        if (!TryComp<SailComponent>(ev.Target, out var sail))
            return;

        var text = "Сложить";
        if (sail.Folded)
            text = "Разложить";
        var verb = new AlternativeVerb()
        {
            Priority = 10,
            Act = () => TryFold(ev.User, ev.Target),
            Impact = LogImpact.Low,
            Text = Loc.GetString(text),
        };
        ev.Verbs.Add(verb);
    }

    private void TryFold(EntityUid playerEntity, EntityUid targetEntity)
    {
        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.15f - _skills.GetSkillLevel(playerEntity, "Intelligence") * 0.15f;
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new SailFoldEvent(),
            targetEntity,
            playerEntity)
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
