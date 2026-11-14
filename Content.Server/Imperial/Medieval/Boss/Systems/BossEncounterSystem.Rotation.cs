using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
    public void StepFinished(EntityUid boss, bool completed)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp) || !comp.FightRunning || !comp.Waiting || comp.Resetting)
            return;
        comp.Waiting = false;
        comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.RunningStep?.RecoveryAfter ?? comp.Recovery);
        if (!completed)
        {
            ClearPending(comp);
            return;
        }
        if (comp.PendingSteps.Count > 0)
        {
            var finished = comp.PendingSteps[comp.PendingIndex];
            if (finished.StoredStrike && finished.Pattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack)
                ClearStoredTargets(comp, finished.Pattern);
            if (finished.SimultaneousPattern is { } simultaneous)
                ClearStoredTargets(comp, simultaneous);
            if (++comp.PendingIndex < comp.PendingSteps.Count)
            {
                comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(finished.RecoveryAfter ?? comp.Recovery);
                return;
            }
            if (comp.PendingMixedStackFirst is { } stackFirst) comp.LastMixedStackFirst = stackFirst;
            ClearPending(comp);
        }
        AdvanceStep(boss, comp);
    }

    private bool StartStep(EntityUid boss, BossEncounterComponent comp)
    {
        var list = ActiveSteps(boss, comp);
        if (list.Count == 0 || comp.Step >= list.Count) return false;
        var step = comp.PendingSteps.Count > 0 ? comp.PendingSteps[comp.PendingIndex] : ChooseStep(boss, list[comp.Step]);
        if (step == null) return false;
        if (comp.PendingSteps.Count == 0 && (step.MixedSentence || step.CrossSentence || step.VigilFollowup != null))
        {
            comp.PendingSteps = EntityManager.System<BossActionSystem>().BuildCombination(boss, comp, step);
            comp.PendingIndex = 0;
            if (comp.PendingSteps.Count == 0) return false;
        }
        if (comp.PendingSteps.Count > 0) step = comp.PendingSteps[comp.PendingIndex];
        var resolvedPattern = comp.Repeating && (comp.Lap & 1) != 0 &&
            step.Alternate is { } alternatePattern ? alternatePattern : step.Pattern;
        var damagePattern = step.Traversal == BossTraversalKind.EdgeCone ? BossTelegraphPattern.CardinalCone : resolvedPattern;
        var damageKey = step.TankBuster ? "buster" : comp.PatternDamage.GetValueOrDefault(damagePattern, step.Damage);
        if (!comp.Damage.TryGetValue(damageKey, out var damage)) return false;
        bool started;
        if (EntityManager.System<BossActionSystem>().Supports(step))
            started = EntityManager.System<BossActionSystem>().Start(boss, comp, step, damage);
        else
        {
            var pattern = resolvedPattern;
            if (step.RandomCorner)
                pattern = (BossTelegraphPattern)((int)BossTelegraphPattern.BlackoutNorthWest + _random.Next(4));
            var name = step.CastName ?? EntityManager.System<BossCastNameSystem>().CastNameKey(boss, pattern);

            if (step.PreloadOnly && step.CastName == null)
                name = EntityManager.System<BossCastNameSystem>().CastNameKey(boss, pattern, sentencing: true);
            var attack = new BossTilePatternAttack
            {
                Telegraph = pattern,
                CastTime = step.CastTime + (step.PreloadOnly || step.StoredStrike ? 2 : 0),
                FollowupDelay = step.FollowupDelay,
                SequentialConstellation = step.SequentialConstellation,
                Damage = damage,
                CastName = name,
                SpreadTargets = step.SpreadTargets
            };
            var targetPattern = step.SimultaneousPattern ?? pattern;
            var remembersTargets = (step.PreloadOnly || step.StoredStrike) &&
                targetPattern is BossTelegraphPattern.Spread or BossTelegraphPattern.Stack;
            IReadOnlyList<EntityUid>? targets = null;
            if (remembersTargets && step.StoredStrike)
            {
                if (!comp.StoredTargets.TryGetValue(targetPattern, out var stored)) return false;
                targets = stored.Targets;
            }
            BossTilePatternAttack? simultaneousAttack = null;
            if (step.SimultaneousPattern is { } targeted)
            {
                if (!comp.Damage.TryGetValue(comp.PatternDamage.GetValueOrDefault(targeted, "sentence"), out var targetedDamage)) return false;
                simultaneousAttack = new BossTilePatternAttack { Telegraph = targeted, Damage = targetedDamage };
            }
            started = EntityManager.System<BossMechanicSystem>().TryStart(boss, attack,
                step.PreloadOnly, step.StoredStrike, step.HideCast, targets, showTargetMarkers: !remembersTargets,
                simultaneousAttack: simultaneousAttack);
            if (started && remembersTargets && step.PreloadOnly)
                RememberTargets(boss, comp, pattern, Comp<BossMechanicComponent>(boss).Targets);
        }
        comp.RunningStep = started ? step : null;
        comp.Waiting = started;
        return started;
    }

    private void UpdateEncounters(float frameTime)
    {
        foreach (var entity in EntityManager.AllEntities<BossEncounterComponent>().ToArray())
        {
            var boss = entity.Owner;
            var comp = entity.Comp;
            if (!TryComp<BossComponent>(boss, out var encounter)) continue;
            if (!comp.FightRunning || MetaData(boss).EntityPaused) continue;
            if (encounter.Finished || encounter.Health <= 0 || !_telegraphs.TryGetArena(boss, out var arena))
            { End(boss); continue; }
            if (_timing.CurTime >= comp.EntryCloses) SetGateLocks(boss, true, true);
            var living = _telegraphs.LivingPlayers(boss);
            if (living.Count == 0)
            {
                if (comp.IdleDeadline == null)
                {
                    comp.IdleDeadline = _timing.CurTime + TimeSpan.FromSeconds(comp.IdleReset);    // give the wipe timer a chance before resetting
                    comp.NextResetAttempt = comp.IdleDeadline.Value;
                    comp.Resetting = true;
                    EntityManager.System<BossSystem>().CancelBossCast(boss);
                    comp.Waiting = false;
                    ClearPending(comp);
                    comp.Resetting = false;
                    SetGateLocks(boss, true, true);
                }
                if (_timing.CurTime >= comp.IdleDeadline && _timing.CurTime >= comp.NextResetAttempt)
                {
                    comp.NextResetAttempt = _timing.CurTime + TimeSpan.FromSeconds(1);
                    Reset(boss, arena);
                }
                continue;
            }
            if (!ValidateStoredTargets(boss, comp)) continue;

            if (comp.IdleDeadline != null)
            {
                comp.IdleDeadline = null;
                comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.Recovery);
                comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
            }
            if (_timing.CurTime >= comp.NextTank)
            {
                comp.NextTank = _timing.CurTime + TimeSpan.FromSeconds(comp.TankInterval);
                if (!EntityManager.System<BossActionSystem>().BlocksTankAttack(boss) &&
                    !(TryComp<BossCastBarComponent>(boss, out var bar) && bar.Casting) &&
                    TryComp<BossFocusMarkerComponent>(boss, out var focus) && focus.Tank is { } tank &&
                    _telegraphs.TryGetPlayerTile(boss, tank, out _, allowCritical: true))
                {
                    if (!EntityManager.System<BossActionSystem>().TankAttack(boss, tank))
                    {
                        _telegraphs.ShowTankStrike(boss, tank);
                        _damage.TryChangeDamage(tank, comp.TankDamage, origin: boss);
                    }
                }
            }
            if (!comp.Waiting && _timing.CurTime >= comp.NextStep &&
                !EntityManager.System<BossActionSystem>().IsBusy(boss))
            {
                if (!StartStep(boss, comp))
                {
                    ClearPending(comp);
                    AdvanceStep(boss, comp);                                    // skip failed steps instead of retrying them forever
                    comp.NextStep = _timing.CurTime + TimeSpan.FromSeconds(comp.Recovery);
                }
            }
        }
    }
}
