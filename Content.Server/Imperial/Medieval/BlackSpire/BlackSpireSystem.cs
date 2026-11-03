using System.Threading;
using System.Threading.Tasks;
using Content.Server.Imperial.Medieval.TempInvincibility;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.BlackSpire;

public sealed partial class BlackSpireSystem : EntitySystem
{
    [Dependency] private readonly BlackSpireCurseSystem _curse = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, CancellationTokenSource> _timers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlackSpireComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlackSpireComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlackSpireComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BlackSpireComponent, TempInvincibilityEndedEvent>(OnInvincibilityEnded);
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

    private void OnMapInit(Entity<BlackSpireComponent> ent, ref MapInitEvent args)
    {
        var cancellation = new CancellationTokenSource();
        _timers.Add(ent.Owner, cancellation);
        _ = AdvanceStagesAsync(ent.Owner, ent.Comp, cancellation.Token);
        _ = RunEffectsAsync(ent.Owner, ent.Comp, cancellation.Token);
        _ = RunAggressionAsync(ent.Owner, ent.Comp, cancellation.Token);
    }

    private void OnShutdown(Entity<BlackSpireComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.DamageAggressionTimer?.Cancel();
        ent.Comp.DamageAggressionTimer?.Dispose();
        ent.Comp.DamageAggressionTimer = null;

        if (!_timers.Remove(ent.Owner, out var cancellation))
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private bool IsActive(EntityUid uid, BlackSpireComponent component)
    {
        return !TerminatingOrDeleted(uid) && TryComp<BlackSpireComponent>(uid, out var current) && current == component;
    }

    private bool IsCharacter(EntityUid uid)
    {
        return HasComp<ActorComponent>(uid) && HasComp<MobStateComponent>(uid) && !HasComp<GhostComponent>(uid) &&
               (HasComp<HumanoidAppearanceComponent>(uid) || HasComp<SkillsComponent>(uid) ||
                (HasComp<BodyComponent>(uid) && HasComp<HandsComponent>(uid) && HasComp<InventoryComponent>(uid)));
    }

    private void OnDamageChanged(Entity<BlackSpireComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !_timers.TryGetValue(ent.Owner, out var cancellation))
            return;

        ent.Comp.Aggressive = true;
        ent.Comp.DamageAggressionActive = true;
        ent.Comp.DamageAggressionTimer?.Cancel();
        ent.Comp.DamageAggressionTimer?.Dispose();
        ent.Comp.DamageAggressionTimer = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        _ = EndDamageAggressionAsync(ent.Owner, ent.Comp, ent.Comp.DamageAggressionTimer);
    }

    private async Task EndDamageAggressionAsync(EntityUid uid, BlackSpireComponent component, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await Timer.Delay(component.DamageAggressionDuration, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (IsActive(uid, component) && component.DamageAggressionTimer == cancellation)
            {
                component.DamageAggressionTimer = null;
                cancellation.Dispose();
                component.DamageAggressionActive = false;
                RefreshAggression(uid, component);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void OnInvincibilityEnded(Entity<BlackSpireComponent> ent, ref TempInvincibilityEndedEvent args)
    {
        if (!_timers.TryGetValue(ent.Owner, out var cancellation))
            return;

        for (var i = 0; i < ent.Comp.InvincibilityEndedSpawnCount; i++)
            SpawnDefender(ent.Owner, ent.Comp, cancellation.Token);
    }
}
