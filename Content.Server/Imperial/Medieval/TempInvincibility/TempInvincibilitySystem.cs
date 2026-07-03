using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.TempInvincibility;

public sealed class TempInvincibilitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TempInvincibilityComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        List<EntityUid>? expired = null;
        var query = EntityQueryEnumerator<TempInvincibilityComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.EndTime > _timing.CurTime)
                continue;

            expired ??= new();
            expired.Add(uid);
        }

        if (expired == null)
            return;

        foreach (var uid in expired)
        {
            if (!TryComp(uid, out TempInvincibilityComponent? component))
                continue;

            EndTempInvincibility(uid, component);
        }
    }

    public void StartTempInvincibility(EntityUid uid, TimeSpan duration)
    {
        var component = EnsureComp<TempInvincibilityComponent>(uid);
        component.EndTime = _timing.CurTime + duration;
    }

    public void EndTempInvincibilityEarly(EntityUid uid, TempInvincibilityComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        RemComp<TempInvincibilityComponent>(uid);
    }

    private void OnBeforeDamageChanged(EntityUid uid, TempInvincibilityComponent component, ref BeforeDamageChangedEvent args)
    {
        if (HasPositiveDamage(args.Damage))
            args.Cancelled = true;
    }

    private void EndTempInvincibility(EntityUid uid, TempInvincibilityComponent component)
    {
        RemComp<TempInvincibilityComponent>(uid);
        RaiseLocalEvent(uid, new TempInvincibilityEndedEvent());
    }

    private static bool HasPositiveDamage(DamageSpecifier damage)
    {
        foreach (var amount in damage.DamageDict.Values)
        {
            if (amount > FixedPoint2.Zero)
                return true;
        }

        return false;
    }
}

public sealed class TempInvincibilityEndedEvent : EntityEventArgs;
