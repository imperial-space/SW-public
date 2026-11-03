using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Medieval.BlackSpire;

public sealed partial class BlackSpireSystem
{
    private async Task AdvanceStagesAsync(EntityUid uid, BlackSpireComponent component, CancellationToken token)
    {
        try
        {
            while (IsActive(uid, component) && component.Stage < 4)
            {
                var index = component.Stage - 1;
                if (index < 0 || index >= component.StageDurations.Count)
                    return;

                await Timer.Delay(component.StageDurations[index], token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!IsActive(uid, component))
                    return;

                component.Stage++;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task RunEffectsAsync(EntityUid uid, BlackSpireComponent component, CancellationToken token)
    {
        try
        {
            while (IsActive(uid, component))
            {
                await Timer.Delay(component.EffectInterval, token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!IsActive(uid, component))
                    return;

                ApplySectorEffects(uid, component, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void ApplySectorEffects(EntityUid uid, BlackSpireComponent component, CancellationToken token)
    {
        if (component.Stage < 2 || Transform(uid).MapUid is not { } map)
            return;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var character, out _, out var xform))
        {
            if (xform.MapUid != map || !IsCharacter(character))
                continue;

            _curse.Apply(character, component.NeedsMultiplier, component.CurseDuration);

            if (component.Stage >= 3 && _random.Prob(component.SkeletonChance))
            {
                TrySpawnSkeleton(uid, component, character, component.WeakSkeleton,
                    component.CharacterSpawnMinRadius, component.CharacterSpawnMaxRadius, token);
            }

            if (component.Stage >= 4)
                _damageable.TryChangeDamage(character, component.RadiationDamage, origin: uid);
        }
    }

    private async Task RunAggressionAsync(EntityUid uid, BlackSpireComponent component, CancellationToken token)
    {
        try
        {
            while (IsActive(uid, component))
            {
                await Timer.Delay(component.AggressionInterval, token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!IsActive(uid, component))
                    return;

                RefreshAggression(uid, component);
                if (component.Aggressive)
                    SpawnDefender(uid, component, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void RefreshAggression(EntityUid uid, BlackSpireComponent component)
    {
        component.Aggressive = component.DamageAggressionActive;
        if (component.Aggressive)
            return;

        foreach (var nearby in _lookup.GetEntitiesInRange(uid, component.AggressionRadius))
        {
            if (!IsCharacter(nearby))
                continue;

            component.Aggressive = true;
            return;
        }
    }
}
