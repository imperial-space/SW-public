using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTraversalSystem
{
    [Dependency] private readonly Robust.Shared.Serialization.Manager.ISerializationManager _rainSerialization = default!;
    partial void ApplyStepParameters(BossEncounterStep step, ref bool valid)
    {
        if (step.Traversal != BossTraversalKind.CrystalRain || step.Parameters == null) return;
        if (!BossMechanicParameters.TryRead<BossRainSettings>(step, _rainSerialization, out var settings) || !settings.IsValid)
        { valid = false; return; }
        step.RainCount = settings.Count;
        step.RainWarningTime = settings.WarningTime;
        step.RainSizes = settings.Sizes;
        step.RainCastTime = settings.CastTime;
        step.RainConcurrent = settings.Concurrent;
        step.RainMaxConcurrent = settings.MaxConcurrent;
        step.RainSpawnInterval = settings.SpawnInterval;
        step.RainMinSpawnInterval = settings.MinSpawnInterval;
        step.RainDoubleChance = settings.DoubleChance;
        step.RainRampSeconds = settings.RampSeconds;
    }

    partial void InitializeRain() => EntityManager.System<BossFeatureSystem>().Register("crystal-rain");

    private void SpawnRainDrop(EntityUid boss, BossTraversalComponent state)
    {
        var size = _random.Pick(state.RainSizes);
        var pattern = size == 2 ? BossTelegraphPattern.CrystalRainSmall : BossTelegraphPattern.CrystalRain;
        var halfSize = Comp<BossArenaMapComponent>(state.Arena).HalfSize;
        var minimum = -halfSize + (size == 3 ? 1 : 0);
        var maximum = halfSize - 1;
        var occupied = state.RainDrops.Where(Exists).Select(drop => Comp<BossTelegraphComponent>(drop).PatternOrigin).ToHashSet();
        var candidates = Enumerable.Range(minimum, maximum - minimum + 1).SelectMany(y =>
            Enumerable.Range(minimum, maximum - minimum + 1).Select(x => new Vector2i(x, y)))
            .Where(tile => tile != state.Origin && !occupied.Contains(tile)).ToList(); // avoid centers already used by active warnings
        if (candidates.Count == 0) return;
        var next = _random.Pick(candidates);
        state.Origin = next;
        state.RainWave++;
        state.RemainingRain--;
        state.RainDrops.Add(_telegraphs.CreateIndependentWarning(boss, pattern, next,
            _timing.CurTime + TimeSpan.FromSeconds(state.RainWarningTime)));
        if (!_telegraphs.HasCrystalAnimation(boss, pattern) && Comp<BossTelegraphSettingsComponent>(boss).FallingCrystalEffect is { } effect)
            state.RainVisuals.Add(Spawn(effect, new EntityCoordinates(state.Grid, next.X + (size == 2 ? 1 : 0.5f), next.Y + (size == 2 ? 1 : 0.5f))));
    }

    private void UpdateRain(EntityUid boss, BossTraversalComponent state)
    {
        var pressure = Math.Clamp((_timing.CurTime - state.RainPressureStart).TotalSeconds / state.RainRampSeconds, 0, 1); // increase rain pressure as the fight continues
        state.RainActiveCap = (int)Math.Floor(state.RainConcurrent + pressure * (state.RainMaxConcurrent - state.RainConcurrent));
        state.RainCurrentInterval = (float)(state.RainSpawnInterval + pressure * (state.RainMinSpawnInterval - state.RainSpawnInterval));
        foreach (var drop in state.RainDrops.ToArray())
        {
            if (!TryComp<BossTelegraphComponent>(drop, out var warning)) { state.RainDrops.Remove(drop); continue; }
            if (_timing.CurTime < warning.EndTime) continue;                     // each crystal has its own warning timer
            state.RainResolved++;
            var impact = _telegraphs.ShowImpact(boss, warning.Danger, warningEntity: drop, playSound: state.RainResolved % 2 == 0);
            if (impact is { } visual && _telegraphs.HasCrystalAnimation(boss, warning.Pattern))
                state.RainVisuals.Add(visual);
            foreach (var player in EntityManager.AllEntities<MobStateComponent>().Select(entity => entity.Owner).ToArray())
            {
                if (!HasComp<BossTraversalComponent>(boss)) return;
                if (_telegraphs.TryGetPlayerTile(boss, player, out var tile, allowCritical: true) && warning.Danger.Contains(tile))
                    _damage.TryChangeDamage(player, state.Damage, origin: boss);
            }
            if (!HasComp<BossTraversalComponent>(boss)) return;
            if (!_telegraphs.HasCrystalAnimation(boss, warning.Pattern) && Comp<BossTelegraphSettingsComponent>(boss).CrystalShatterEffect is { } effect)
            {
                var offset = warning.Pattern == BossTelegraphPattern.CrystalRainSmall ? 1 : 0.5f;
                state.RainVisuals.Add(Spawn(effect, new EntityCoordinates(state.Grid, warning.PatternOrigin.X + offset, warning.PatternOrigin.Y + offset)));
            }
            if (Exists(drop) && !Terminating(drop)) Del(drop);
            state.RainDrops.Remove(drop);
        }
        if (state.RemainingRain > 0 && _timing.CurTime >= state.Deadline)
        {
            var count = _random.Prob(state.RainDoubleChance) ? 2 : 1;
            for (var i = 0; i < count && state.RemainingRain > 0 && state.RainDrops.Count < state.RainActiveCap; i++)
                SpawnRainDrop(boss, state);
            state.Deadline = _timing.CurTime + TimeSpan.FromSeconds(state.RainCurrentInterval);
        }
        if (state.RemainingRain == 0 && state.RainDrops.Count == 0)              // finish after all crystal warnings have landed
        {
            state.Completed = true;
            RemComp<BossTraversalComponent>(boss);
        }
    }

    partial void TickRain(EntityUid boss, BossTraversalComponent state, ref bool handled)
    {
            if (state.Kind == BossTraversalKind.CrystalRain)
            {
                if (state.Phase == BossTraversalPhase.Preparing && _timing.CurTime >= state.Deadline)
                {
                    state.Phase = BossTraversalPhase.Rain;
                    var rotation = Comp<BossEncounterComponent>(boss);
                    rotation.RainPressureStart ??= _timing.CurTime;
                    state.RainPressureStart = rotation.RainPressureStart.Value;
                    var bar = Comp<BossCastBarComponent>(boss);
                    bar.Casting = false;
                    bar.ClearCastName();
                    Dirty(boss, bar);
                }
                if (state.Phase == BossTraversalPhase.Rain) UpdateRain(boss, state);
                handled = true;
            }
    }
}
