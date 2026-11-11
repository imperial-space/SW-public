using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Weapons.Melee;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

public sealed partial class AdvancedLightAttackSystem
{
    private void InitializeDualWielding()
    {

        SubscribeLocalEvent<HandsComponent, DidEquipHandEvent>(OnDualWieldEquipped);
        SubscribeLocalEvent<HandsComponent, DidUnequipHandEvent>(OnDualWieldUnequipped);
        SubscribeLocalEvent<AdvancedLightAttackUserComponent, HandCountChangedEvent>(OnDualWieldHandsChanged);

        if (_net.IsServer)
            SubscribeLocalEvent<HandsComponent, ComponentShutdown>(OnDualWieldHandsShutdown);

        if (_net.IsClient)
            SubscribeLocalEvent<MeleeWeaponComponent, ComponentStartup>(OnDualWieldMeleeStartup);
        SubscribeLocalEvent<MeleeWeaponComponent, ComponentShutdown>(OnDualWieldMeleeShutdown);
    }

    public bool RefreshDualWielding(EntityUid user)
    {
        return RefreshDualWielding(user, null);
    }

    private bool RefreshDualWielding(EntityUid user, EntityUid? removing)
    {
        var count = 0;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held != removing && !HasComp<VirtualItemComponent>(held) &&
                TryComp<MeleeWeaponComponent>(held, out var melee) && !melee.MustBeEquippedToUse && ++count >= 2)
                break;
        }

        var dual = count >= 2;
        var component = EnsureComp<AdvancedLightAttackUserComponent>(user);
        SetDualWielding(user, component, dual);
        return dual;
    }

    private void SetDualWielding(EntityUid user, AdvancedLightAttackUserComponent component, bool dual)
    {
        if (component.IsDualWielding == dual)
            return;
        component.IsDualWielding = dual;
        Dirty(user, component);
    }

    private bool SimulatesDualWielding(EntityUid user)
    {
        return !TerminatingOrDeleted(user) && (_net.IsServer || _players.LocalEntity == user);
    }


    private void OnDualWieldEquipped(EntityUid user, HandsComponent hands, DidEquipHandEvent args)
    {
        if (SimulatesDualWielding(user) && (HasComp<AdvancedLightAttackUserComponent>(user) || HasComp<AdvancedLightAttackComponent>(args.Equipped)))
            RefreshDualWielding(user);
    }

    private void OnDualWieldUnequipped(EntityUid user, HandsComponent hands, DidUnequipHandEvent args)
    {
        if (SimulatesDualWielding(user) && HasComp<AdvancedLightAttackUserComponent>(user))
            RefreshDualWielding(user);
    }

    private void OnDualWieldHandsChanged(EntityUid user, AdvancedLightAttackUserComponent component, HandCountChangedEvent args)
    {
        if (SimulatesDualWielding(user))
            RefreshDualWielding(user);
    }

    private void OnDualWieldHandsShutdown(EntityUid user, HandsComponent hands, ComponentShutdown args)
    {
        ClearDualWielding(user);
    }


    /// <summary>Clear the existing flag when the simulated user's hands disappear.</summary>
    public void ClearDualWielding(EntityUid user)
    {
        if (SimulatesDualWielding(user) && TryComp<AdvancedLightAttackUserComponent>(user, out var component))
            SetDualWielding(user, component, false);
    }

    private void OnDualWieldMeleeStartup(EntityUid weapon, MeleeWeaponComponent melee, ComponentStartup args)
    {
        RefreshHeldMelee(weapon, removing: false);
    }


    private void OnDualWieldMeleeShutdown(EntityUid weapon, MeleeWeaponComponent melee, ComponentShutdown args)
    {
        RefreshHeldMelee(weapon, removing: true);
    }

    public void RefreshHeldMelee(EntityUid weapon, bool removing)
    {
        if (!TryComp(weapon, out TransformComponent? transform))
            return;
        var user = transform.ParentUid;
        if (SimulatesDualWielding(user) && HasComp<AdvancedLightAttackUserComponent>(user) && _hands.IsHolding(user, weapon))
            // ComponentShutdown runs before the component disappears from queries.
            RefreshDualWielding(user, removing ? weapon : null);
    }
}
