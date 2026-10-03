using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>Stops voluntary movement and attacks while training uses the standard DoAfter cancellation rules.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BookTamingLockComponent : Component;

[Serializable, NetSerializable]
public sealed partial class BookTamingDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Uses the same movement gate on the server and predicted client.</summary>
public sealed class BookTamingLockSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BookTamingLockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BookTamingLockComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BookTamingLockComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<BookTamingLockComponent, AttackAttemptEvent>(OnAttack);
    }

    private void OnStartup(Entity<BookTamingLockComponent> ent, ref ComponentStartup args) => _blocker.UpdateCanMove(ent);

    private void OnShutdown(Entity<BookTamingLockComponent> ent, ref ComponentShutdown args) => _blocker.UpdateCanMove(ent);

    private void OnCanMove(EntityUid uid, BookTamingLockComponent comp, UpdateCanMoveEvent args)
    {
        if (comp.LifeStage < ComponentLifeStage.Stopping)
            args.Cancel();
    }

    private void OnAttack(EntityUid uid, BookTamingLockComponent comp, AttackAttemptEvent args)
    {
        if (comp.LifeStage < ComponentLifeStage.Stopping)
            args.Cancel();
    }
}
