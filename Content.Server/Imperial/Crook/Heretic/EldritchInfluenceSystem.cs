using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic.Systems;

public sealed class EldritchInfluenceSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EldritchInfluenceComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<EldritchInfluenceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<EldritchInfluenceComponent, EldritchInfluenceDoAfterEvent>(OnDoAfter);
    }

    private void OnInteract(EntityUid uid, EldritchInfluenceComponent comp, InteractHandEvent args)
    {
        if (args.Handled || comp.Spent)
            return;

        args.Handled = StartDraining(uid, args.User);
    }

    private void OnInteractUsing(EntityUid uid, EldritchInfluenceComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || comp.Spent)
            return;

        args.Handled = StartDraining(uid, args.User, args.Used);
    }

    private bool StartDraining(EntityUid uid, EntityUid user, EntityUid? tool = null)
{
    if (!TryComp<EldritchInfluenceComponent>(uid, out var comp) || comp.Spent)
        return false;

    if (!HasComp<HereticComponent>(user))
    {
        _damageable.TryChangeDamage(user, comp.RejectionDamage);
        _popup.PopupEntity(Loc.GetString(comp.RejectionMessage), user, user);
        return true;
    }

    var time = comp.BaseDrainTime;
    if (tool != null && TryComp<EldritchInfluenceDrainerComponent>(tool.Value, out var drainer))
    {
        time = TimeSpan.FromSeconds(comp.BaseDrainTime.TotalSeconds * drainer.TimeModifier.TotalSeconds);
    }

    var args = new DoAfterArgs(EntityManager, user, time,
        new EldritchInfluenceDoAfterEvent(), uid, target: uid, used: tool)
    {
        BreakOnDamage = true,
        BreakOnMove = true,
        NeedHand = true,
        Broadcast = false,
        BreakOnWeightlessMove = false,
        Hidden = true
    };

    _popup.PopupEntity(Loc.GetString(comp.StartDrainingMessage), user, user);
    return _doAfter.TryStartDoAfter(args);
}

    private void OnDoAfter(EntityUid uid, EldritchInfluenceComponent comp, EldritchInfluenceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || comp.Spent)
            return;

        comp.Spent = true;

        var coords = Transform(uid).Coordinates;
        Spawn(comp.SpawnOnDrain, coords);
        Del(uid);
    }
}
