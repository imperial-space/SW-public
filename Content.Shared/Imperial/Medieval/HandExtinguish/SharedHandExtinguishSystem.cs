using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.HandExtinguish;

public sealed class SharedHandExtinguishSystem : EntitySystem
{
    private const float FireStackReduction = -0.5f; // how much a fire stack should change on hand interaction

    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlammableComponent, InteractHandEvent>(
            OnInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnInteractHand(EntityUid uid, FlammableComponent flammable, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (args.User == args.Target)
            return;

        var onFire = flammable.OnFire;

        if (_netManager.IsClient &&
            TryComp<AppearanceComponent>(uid, out var appearance) &&
            _appearance.TryGetData(uid, FireVisuals.OnFire, out bool visualOnFire, appearance))
        {
            onFire = visualOnFire;
        }

        if (!onFire || !flammable.CanExtinguish)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        args.Handled = true;

        var targetName = Identity.Name(uid, EntityManager, args.User);
        _popupSystem.PopupClient($"Вы тушите {targetName}", uid, args.User);

        if (_netManager.IsServer)
        {
            string? othersMessage = null;
            if (TryComp<InteractionPopupComponent>(uid, out var component) &&
                !string.IsNullOrEmpty(component.MessagePerceivedByOthers))
            {
                othersMessage = Loc.GetString(component.MessagePerceivedByOthers,
                    ("user", Identity.Entity(args.User, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager)));
            }
            else
            {
                var userName = Identity.Name(args.User, EntityManager);
                var targetNameOthers = Identity.Name(uid, EntityManager);
                othersMessage = $"{userName} тушит {targetNameOthers}";
            }

            _popupSystem.PopupEntity(othersMessage, uid, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
        }

        if (!_netManager.IsServer)
            return;

        flammable.FireStacks +=  FireStackReduction;
        Dirty(uid, flammable);
    }
}
