using System.Threading;
using System.Threading.Tasks;
using Content.Server.Imperial.Medieval.NeedSleep;
using Content.Shared.Imperial.Medieval.BlackSpire;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.BlackSpire;

public sealed class BlackSpireCurseSystem : EntitySystem
{
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;

    private readonly Dictionary<EntityUid, CancellationTokenSource> _timers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlackSpireCurseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlackSpireCurseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlackSpireCurseComponent, GetSleepLevelModifiersEvent>(OnSleepLevel);
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

    public void Apply(EntityUid uid, float multiplier, TimeSpan duration)
    {
        var component = EnsureComp<BlackSpireCurseComponent>(uid);
        component.Multiplier = multiplier;
        component.Duration = duration;
        component.Active = true;
        Dirty(uid, component);
        StartTimer(uid, component);
    }

    private void OnStartup(Entity<BlackSpireCurseComponent> ent, ref ComponentStartup args)
    {
        StartTimer(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<BlackSpireCurseComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Active = false;
        StopTimer(ent.Owner);
    }

    private void OnSleepLevel(Entity<BlackSpireCurseComponent> ent, ref GetSleepLevelModifiersEvent args)
    {
        if (ent.Comp.Active && !args.Sleeping)
            args.Modifier *= ent.Comp.Multiplier;
    }

    private void StartTimer(EntityUid uid, BlackSpireCurseComponent component)
    {
        StopTimer(uid);
        component.LastHunger = TryComp<HungerComponent>(uid, out var hunger)
            ? _hunger.GetHunger(hunger)
            : float.NaN;
        component.LastThirst = TryComp<ThirstComponent>(uid, out var thirst)
            ? thirst.CurrentThirst
            : float.NaN;
        var cancellation = new CancellationTokenSource();
        _timers.Add(uid, cancellation);
        _ = ExpireAsync(uid, component, cancellation.Token);
        _ = AccelerateNeedsAsync(uid, component, cancellation.Token);
    }

    private async Task AccelerateNeedsAsync(EntityUid uid, BlackSpireCurseComponent component, CancellationToken token)
    {
        try
        {
            while (component.Active && !TerminatingOrDeleted(uid))
            {
                await Timer.Delay(TimeSpan.FromSeconds(1), token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!TryComp<BlackSpireCurseComponent>(uid, out var current) || current != component)
                    return;

                var bonusMultiplier = component.Multiplier - 1f;
                if (bonusMultiplier <= 0f)
                    continue;

                if (TryComp<HungerComponent>(uid, out var hunger))
                {
                    var currentHunger = _hunger.GetHunger(hunger);
                    var hungerLoss = float.IsNaN(component.LastHunger)
                        ? 0f
                        : Math.Max(0f, component.LastHunger - currentHunger);
                    component.LastHunger = currentHunger;
                    if (hungerLoss > 0f)
                    {
                        _hunger.ModifyHunger(uid, -hungerLoss * bonusMultiplier, hunger);
                        component.LastHunger = _hunger.GetHunger(hunger);
                    }
                }

                if (TryComp<ThirstComponent>(uid, out var thirst))
                {
                    var currentThirst = thirst.CurrentThirst;
                    var thirstLoss = float.IsNaN(component.LastThirst)
                        ? 0f
                        : Math.Max(0f, component.LastThirst - currentThirst);
                    component.LastThirst = currentThirst;
                    if (thirstLoss > 0f)
                    {
                        _thirst.ModifyThirst(uid, thirst, -thirstLoss * bonusMultiplier);
                        component.LastThirst = thirst.CurrentThirst;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void StopTimer(EntityUid uid)
    {
        if (!_timers.Remove(uid, out var cancellation))
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task ExpireAsync(EntityUid uid, BlackSpireCurseComponent component, CancellationToken token)
    {
        try
        {
            await Timer.Delay(component.Duration, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!TerminatingOrDeleted(uid) && TryComp<BlackSpireCurseComponent>(uid, out var current) && current == component)
                RemComp<BlackSpireCurseComponent>(uid);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
