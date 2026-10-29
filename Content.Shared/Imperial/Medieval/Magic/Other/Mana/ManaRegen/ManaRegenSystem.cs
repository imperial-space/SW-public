using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Imperial.Medieval.Magic.ManaRegen;


public sealed partial class ManaRegenSystem : EntitySystem
{
    [Dependency] private readonly ManaSystem _manaSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ManaRegenComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<ManaRegenComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<ManaRegenComponent, GotUnequippedEvent>(OnUnequip);
        SubscribeLocalEvent<ManaRegenComponent, ComponentShutdown>(OnShutdown);
    }
    // equiping and unequiping remove and add there mana regen from the total regen pool.
    private void OnEquip(
    EntityUid uid,
    ManaRegenComponent component,
    GotEquippedEvent args)
    {
        if (component.Equipee is { } oldEquipee &&
        oldEquipee != args.Equipee)
        {
            _manaSystem.RemovePassiveManaChange(oldEquipee, uid);
        }

        component.Equipee = args.Equipee;

        _manaSystem.SetPassiveManaChange(args.Equipee, uid, component.Regen);
    }

    private void OnUnequip(
    EntityUid uid,
    ManaRegenComponent component,
    GotUnequippedEvent args)
    {
        _manaSystem.RemovePassiveManaChange(
        args.Equipee,
        uid);

        component.Equipee = null;
    }

    private void OnShutdown(
    EntityUid uid,
    ManaRegenComponent component,
    ComponentShutdown args)
    {
        if (component.Equipee is not { } equipee)
            return;

        _manaSystem.RemovePassiveManaChange(equipee, uid);
    }
    // race mana regen modifier aslo applies to items, not intended but i dont feel like fixing it.
    private void OnExamine(
    EntityUid uid,
    ManaRegenComponent component,
    ExaminedEvent args)
    {
        var regenMultiplier = ManaComponent.DefaultRegenMultiplier;
        var raceMultiplier = 1f;

        if (TryComp<ManaComponent>(args.Examiner, out var manaComponent))
        {
            regenMultiplier = manaComponent.RegenMultiplier;
            raceMultiplier = manaComponent.RegenRaceModifier;
        }

        var regen = component.Regen * regenMultiplier * raceMultiplier;

        args.PushMarkup(Loc.GetString(component.ManaRegenMessage, ("regen", Math.Round(regen, 3)
        )));
    }
}
