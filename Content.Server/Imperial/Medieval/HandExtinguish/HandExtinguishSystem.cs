using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.Medieval.HandExtinguish;

public sealed class HandExtinguishSystem : EntitySystem
{
    private const float FireStackReduction = -0.5f;

    [Dependency] private readonly FlammableSystem _flammableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlammableComponent, InteractHandEvent>(
            OnInteractHand,
            before:
            [
                typeof(Content.Shared.Interaction.InteractionPopupSystem)
            ]);
    }

    private void OnInteractHand(EntityUid uid, FlammableComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.User == args.Target)
            return;

        if (!component.OnFire || !component.CanExtinguish)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        args.Handled = true;
        _flammableSystem.AdjustFireStacks(args.Target, FireStackReduction, component);
    }
}
