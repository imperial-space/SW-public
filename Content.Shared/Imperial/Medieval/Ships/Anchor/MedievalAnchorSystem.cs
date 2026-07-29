using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

public sealed class MedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, MedievalAnchorComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !TryStartUse(uid, component, args.User))
            return;

        args.Handled = true;
    }

    private bool TryStartUse(EntityUid uid, MedievalAnchorComponent component, EntityUid user)
    {
        if (component.ActiveUser != null || !_skills.HasSkill(user, SharedSkillsSystem.StrengthId))
            return false;

        if (_net.IsServer)
            SetActiveUser(uid, component, user);

        var doAfter = new DoAfterArgs(EntityManager,
            user,
            GetUseTime(user, component),
            new ToggleAnchorEvent(),
            uid,
            target: uid)
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

        if (_doAfter.TryStartDoAfter(doAfter))
            return true;

        if (_net.IsServer)
            SetActiveUser(uid, component, null);

        return false;
    }

    private float GetUseTime(EntityUid user, MedievalAnchorComponent component)
    {
        var strength = _skills.GetSkillLevel(user, SharedSkillsSystem.StrengthId);
        var useTime = MathF.Max(1f, component.BaseUseTime - strength * component.StrengthUseTimeModifier);
        return component.Lowered ? useTime : useTime * component.LoweringTimeMultiplier;
    }

    private void SetActiveUser(EntityUid uid, MedievalAnchorComponent component, EntityUid? user)
    {
        component.ActiveUser = user;
        Dirty(uid, component);
    }
}
