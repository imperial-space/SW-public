using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.Events;
using Content.Shared.Interaction;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Heretic.Systems;

public sealed class EldritchInfluenceSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doafter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const int SlashDamage = 50;

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
        // Создаем урон по типу "Slash" (порезы)
        var damage = new DamageSpecifier();
        if (_prototype.TryIndex<DamageTypePrototype>("Slash", out var slash))
        {
            damage.DamageDict.Add("Slash", SlashDamage);
            _damageable.TryChangeDamage(user, damage);
            _popup.PopupEntity(Loc.GetString("Нечто отвергает ваше вмешательство!"), uid, user);
            return true;
        }
        return false; // Добавлено: возвращаем false если прототип не найден
    }

    var time = TryComp<EldritchInfluenceDrainerComponent>(tool, out var drainer)
        ? drainer.Time
        : 10f;

    var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time),
        new EldritchInfluenceDoAfterEvent(), uid, target: uid, used: tool)
    {
        BreakOnDamage = true,
        BreakOnMove = true,
        NeedHand = true
    };

    _popup.PopupEntity(Loc.GetString("Вы начинаете поглощать знания из сдвига..."), uid, user);
    return _doafter.TryStartDoAfter(args);
}

    private void OnDoAfter(EntityUid uid, EldritchInfluenceComponent comp, EldritchInfluenceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || comp.Spent)
            return;

        comp.Spent = true;
        Dirty(uid, comp);

        var coords = Transform(uid).Coordinates;
        Spawn("EldritchInfluenceIntermediate", coords);
        Del(uid);
    }
}
