using Content.Shared.Imperial.Medieval.ChargeableAnnounce;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.ChargeableAnnounce;

public sealed class ChargeableAnnounceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChargeableAnnounceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ChargeableAnnounceComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(EntityUid uid, ChargeableAnnounceComponent comp, ComponentStartup args)
    {
        UpdateVisuals(uid, comp);
    }

    private void OnAfterAutoHandleState(EntityUid uid, ChargeableAnnounceComponent comp, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(uid, comp);
    }

    private void UpdateVisuals(EntityUid uid, ChargeableAnnounceComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var state = comp.IsCharged
            ? comp.ChargedState
            : comp.UnchargedState;

        if (string.IsNullOrWhiteSpace(state))
            return;

        sprite.LayerSetState(0, state);
    }
}
