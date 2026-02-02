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
    private static readonly System.TimeSpan UserInteractCooldown = System.TimeSpan.FromSeconds(0.5);

    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    private readonly System.Collections.Generic.Dictionary<EntityUid, System.TimeSpan> _lastInteractByUser = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlammableComponent, InteractHandEvent>(
            OnInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnInteractHand(EntityUid uid, FlammableComponent flammable, InteractHandEvent args)
    {
        if (args.Handled || args.User == args.Target || !HasComp<HandsComponent>(args.User))
            return;

        var now = _gameTiming.CurTime;
        if (_lastInteractByUser.TryGetValue(args.User, out var last) && now < last + UserInteractCooldown)
        {
            args.Handled = true;
            return;
        }

        if (_netManager.IsClient &&
            TryComp<AppearanceComponent>(uid, out var appearance) &&
            _appearance.TryGetData(uid, FireVisuals.OnFire, out bool visualOnFire, appearance))
        {
            if (!visualOnFire)
                return;

            args.Handled = true;
            var targetName = Identity.Name(uid, EntityManager, args.User);
            _popupSystem.PopupClient($"Вы тушите {targetName}", uid, args.User);
            _lastInteractByUser[args.User] = now;
        }

        var onFire = flammable.OnFire;
        if (!onFire || !flammable.CanExtinguish)
            return;
        _lastInteractByUser[args.User] = now;
        args.Handled = true;
        if (_netManager.IsServer)
        {
            string? othersMessage = null;
            var userName = Identity.Name(args.User, EntityManager);
            var targetNameOthers = Identity.Name(uid, EntityManager);
            othersMessage = $"{userName} тушит {targetNameOthers}";
            _popupSystem.PopupEntity(othersMessage, uid, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
            flammable.FireStacks +=  FireStackReduction;
            Dirty(uid, flammable);
        }

        if (!_netManager.IsServer)
            return;


    }
}
