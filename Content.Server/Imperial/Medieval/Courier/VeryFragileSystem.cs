using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.Stamina;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Courier;

public sealed class VeryFragileSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly HashSet<EntityUid> _propagatingDamage = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VeryFragileComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VeryFragileComponent, GotEquippedHandEvent>(OnFragileEquippedHand);
        SubscribeLocalEvent<VeryFragileComponent, GotEquippedEvent>(OnFragileEquipped);
        SubscribeLocalEvent<VeryFragileComponent, EntGotInsertedIntoContainerMessage>(OnFragileInsertedIntoContainer);
        SubscribeLocalEvent<VeryFragileComponent, EntGotRemovedFromContainerMessage>(OnFragileRemovedFromContainer);

        SubscribeLocalEvent<ContainerManagerComponent, GotEquippedHandEvent>(OnContainerEquippedHand);
        SubscribeLocalEvent<ContainerManagerComponent, GotUnequippedHandEvent>(OnContainerUnequippedHand);
        SubscribeLocalEvent<ContainerManagerComponent, GotEquippedEvent>(OnContainerEquipped);
        SubscribeLocalEvent<ContainerManagerComponent, GotUnequippedEvent>(OnContainerUnequipped);
        SubscribeLocalEvent<ContainerManagerComponent, EntGotInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<ContainerManagerComponent, EntGotRemovedFromContainerMessage>(OnContainerRemoved);

        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnCarrierDamageChanged);
        SubscribeLocalEvent<StaminaComponent, GetStaminaCritDurationModifiersEvent>(OnCarrierStaminaCrit);
    }

    private void OnMapInit(Entity<VeryFragileComponent> ent, ref MapInitEvent args)
    {
        SetCarrier(ent, ResolveCarrier(ent.Owner));
    }

    private void OnFragileEquippedHand(Entity<VeryFragileComponent> ent, ref GotEquippedHandEvent args)
    {
        SetCarrier(ent, args.User);
    }

    private void OnFragileEquipped(Entity<VeryFragileComponent> ent, ref GotEquippedEvent args)
    {
        SetCarrier(ent, args.Equipee);
    }

    private void OnFragileInsertedIntoContainer(Entity<VeryFragileComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        SetCarrier(ent, ResolveCarrier(ent.Owner));
    }

    private void OnFragileRemovedFromContainer(Entity<VeryFragileComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        SetCarrier(ent, null);
    }

    private void OnContainerEquippedHand(EntityUid uid, ContainerManagerComponent _, GotEquippedHandEvent args)
    {
        RefreshContainedFragileCarriers(uid);
    }

    private void OnContainerUnequippedHand(EntityUid uid, ContainerManagerComponent _, GotUnequippedHandEvent args)
    {
        RefreshContainedFragileCarriers(uid);
    }

    private void OnContainerEquipped(EntityUid uid, ContainerManagerComponent _, GotEquippedEvent args)
    {
        RefreshContainedFragileCarriers(uid);
    }

    private void OnContainerUnequipped(EntityUid uid, ContainerManagerComponent _, GotUnequippedEvent args)
    {
        RefreshContainedFragileCarriers(uid);
    }

    private void OnContainerInserted(Entity<ContainerManagerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        RefreshContainedFragileCarriers(ent.Owner);
    }

    private void OnContainerRemoved(Entity<ContainerManagerComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        RefreshContainedFragileCarriers(ent.Owner);
    }

    private void OnCarrierDamageChanged(EntityUid uid, DamageableComponent _, ref DamageChangedEvent args)
    {
        if (args.DamageDelta == null ||
            !args.DamageIncreased ||
            _propagatingDamage.Contains(uid))
        {
            return;
        }

        var positiveDamage = DamageSpecifier.GetPositive(args.DamageDelta);
        if (positiveDamage.Empty)
            return;

        var query = EntityQueryEnumerator<VeryFragileComponent>();
        while (query.MoveNext(out var fragileUid, out var fragile))
        {
            if (fragile.Carrier != uid ||
                TerminatingOrDeleted(fragileUid))
            {
                continue;
            }

            ApplyDamageToFragile(fragileUid, positiveDamage, uid);
        }
    }

    private void OnCarrierStaminaCrit(EntityUid uid, StaminaComponent _, ref GetStaminaCritDurationModifiersEvent args)
    {
        var query = EntityQueryEnumerator<VeryFragileComponent>();
        while (query.MoveNext(out var fragileUid, out var fragile))
        {
            if (fragile.Carrier != uid ||
                TerminatingOrDeleted(fragileUid) ||
                fragile.StaminaCritDamage.Empty)
            {
                continue;
            }

            ApplyDamageToFragile(fragileUid, fragile.StaminaCritDamage, uid);
        }
    }

    private void ApplyDamageToFragile(EntityUid fragileUid, DamageSpecifier damage, EntityUid? origin)
    {
        if (damage.Empty)
            return;

        _propagatingDamage.Add(fragileUid);
        try
        {
            _damageable.TryChangeDamage(fragileUid, new DamageSpecifier(damage), origin: origin);
        }
        finally
        {
            _propagatingDamage.Remove(fragileUid);
        }
    }

    private void RefreshContainedFragileCarriers(EntityUid root)
    {
        if (!TryComp<ContainerManagerComponent>(root, out _))
            return;

        var stack = new Stack<EntityUid>();
        var visited = new HashSet<EntityUid>();

        stack.Push(root);

        while (stack.TryPop(out var current))
        {
            if (!visited.Add(current) ||
                !TryComp<ContainerManagerComponent>(current, out var containers))
            {
                continue;
            }

            foreach (var container in containers.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (TryComp<VeryFragileComponent>(contained, out var fragile))
                        SetCarrier((contained, fragile), ResolveCarrier(contained));

                    if (HasComp<ContainerManagerComponent>(contained))
                        stack.Push(contained);
                }
            }
        }
    }

    private EntityUid? ResolveCarrier(EntityUid uid)
    {
        var current = uid;

        while (_container.TryGetContainingContainer(current, out var containing))
        {
            current = containing.Owner;
            if (IsCarrierEntity(current))
                return current;
        }

        return null;
    }

    private bool IsCarrierEntity(EntityUid uid)
    {
        return HasComp<HandsComponent>(uid) || HasComp<InventoryComponent>(uid);
    }

    private static void SetCarrier(Entity<VeryFragileComponent> ent, EntityUid? carrier)
    {
        ent.Comp.Carrier = carrier;
    }
}
