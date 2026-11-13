using Content.Client.Hands.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

/// <summary>Use the native hands notifications without claiming its lifecycle subscriptions.</summary>
public sealed class AdvancedLightAttackHandsSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly AdvancedLightAttackSystem _advanced = default!;

    private EntityUid? _handsOwner;

    public override void Initialize()
    {
        base.Initialize();
        _hands.OnPlayerHandsAdded += OnHandsAdded;
        _hands.OnPlayerHandsRemoved += OnHandsRemoved;
        if (_hands.TryGetPlayerHands(out var hands) && hands is { } current)
            _handsOwner = current.Owner;
    }

    public override void Shutdown()
    {
        _hands.OnPlayerHandsAdded -= OnHandsAdded;
        _hands.OnPlayerHandsRemoved -= OnHandsRemoved;
        _handsOwner = null;
        base.Shutdown();
    }

    private void OnHandsAdded(Entity<HandsComponent> hands)
    {
        _handsOwner = hands.Owner;
    }

    private void OnHandsRemoved()
    {
        if (_handsOwner is { } user)
            _advanced.ClearDualWielding(user);
        _handsOwner = null;
    }
}
