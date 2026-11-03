using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.TempInvincibility;

public sealed class TempInvincibilitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, CancellationTokenSource> _timers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TempInvincibilityComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<TempInvincibilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TempInvincibilityComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Cancel();
            timer.Dispose();
        }

        _timers.Clear();
        base.Shutdown();
    }

    private void OnStartup(Entity<TempInvincibilityComponent> ent, ref ComponentStartup args)
    {
        StartTimer(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<TempInvincibilityComponent> ent, ref ComponentShutdown args)
    {
        StopTimer(ent.Owner);
    }

    public void StartTempInvincibility(EntityUid uid, TimeSpan duration)
    {
        var component = EnsureComp<TempInvincibilityComponent>(uid);
        component.EndTime = _timing.CurTime + duration;
        StartTimer(uid, component);
    }

    private void StartTimer(EntityUid uid, TempInvincibilityComponent component)
    {
        StopTimer(uid);
        var cancellation = new CancellationTokenSource();
        _timers.Add(uid, cancellation);
        _ = EndInvincibilityAsync(uid, component, cancellation.Token);
    }

    private void StopTimer(EntityUid uid)
    {
        if (!_timers.Remove(uid, out var cancellation))
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task EndInvincibilityAsync(EntityUid uid, TempInvincibilityComponent component, CancellationToken token)
    {
        try
        {
            var remaining = component.EndTime - _timing.CurTime;
            await Timer.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!TerminatingOrDeleted(uid) && TryComp<TempInvincibilityComponent>(uid, out var current) && current == component)
                EndTempInvincibility(uid, component);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
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
