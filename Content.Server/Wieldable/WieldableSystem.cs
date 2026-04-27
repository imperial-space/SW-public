using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Server.Wieldable;

public sealed class WieldableSystem : SharedWieldableSystem
{
    [Dependency] private readonly ContentEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, ItemUnwieldedEvent>(OnEyeOffsetUnwielded);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, ItemWieldedEvent>(OnEyeOffsetWielded);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, HeldRelayedEvent<GetEyePvsScaleRelayedEvent>>(OnGetEyePvsScale);
    }

    private void OnEyeOffsetUnwielded(Entity<CursorOffsetRequiresWieldComponent> entity, ref ItemUnwieldedEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnEyeOffsetWielded(Entity<CursorOffsetRequiresWieldComponent> entity, ref ItemWieldedEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnGetEyePvsScale(Entity<CursorOffsetRequiresWieldComponent> entity,
        ref HeldRelayedEvent<GetEyePvsScaleRelayedEvent> args)
    {
        if (!TryComp(entity, out EyeCursorOffsetComponent? eyeCursorOffset) || !TryComp(entity.Owner, out WieldableComponent? wieldableComp))
            return;

        if (IsRestrictedOutsideSea(entity))
            return;

        if (!wieldableComp.Wielded)
            return;

        args.Args.Scale += eyeCursorOffset.PvsIncrease;
    }

    private bool IsRestrictedOutsideSea(EntityUid uid)
    {
        if (!HasComp<EyeCursorOffsetShipResrtictComponent>(uid))
            return false;

        var mapUid = Transform(uid).MapUid;
        return !mapUid.HasValue || !HasComp<SeaComponent>(mapUid.Value);
    }
}
