using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.HandExtinguish;

public sealed class SharedHandExtinguishSystem : EntitySystem
{
    private const float FireStackReduction = -0.5f; // how much a fire stack should change on hand interaction

    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlammableComponent, InteractHandEvent>(
            OnInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnInteractHand(EntityUid uid, FlammableComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.User == args.Target)
            return;

        var onFire = component.OnFire;

        if (_netManager.IsClient &&
            TryComp<AppearanceComponent>(uid, out var appearance) &&
            _appearance.TryGetData(uid, FireVisuals.OnFire, out bool visualOnFire, appearance))
        {
            onFire = visualOnFire;
        }

        if (!onFire || !component.CanExtinguish)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        args.Handled = true;

        if (!_netManager.IsServer)
            return;

        component.FireStacks +=  FireStackReduction;
        Dirty(uid, component);
    }
}
